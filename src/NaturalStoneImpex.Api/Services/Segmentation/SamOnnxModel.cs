using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NaturalStoneImpex.Api.Services.Segmentation;

/// <summary>
/// MobileSAM via ONNX Runtime on CPU.
///
/// Model source: Hugging Face repo "vietanhdev/segment-anything-onnx-models"
/// (mobile_sam_20230629.zip -&gt; mobile_sam.encoder.onnx + sam_vit_h_4b8939.decoder.onnx),
/// produced by the samexporter project (Apache-2.0, exported with --use-preprocess) and
/// renamed by scripts/download-visualizer-models.ps1 to mobilesam-encoder.onnx /
/// mobilesam-decoder.onnx. See that script for full provenance details.
///
/// TENSOR-CONTRACT DEVIATION FROM THE STANDARD SAM ONNX EXPORT (documented per task
/// brief instructions — verified via InputMetadata/OutputMetadata of the real models):
/// - Encoder input "input_image" is float32 HWC with DYNAMIC height/width, i.e. rank 3
///   [-1, -1, 3] — NOT the usual batched, channel-first, fixed 1024x1024
///   [1, 3, 1024, 1024] tensor. This export was built with samexporter's
///   `--use-preprocess` flag, which bakes ImageNet mean/std normalization AND the
///   1024x1024 padding into the ONNX graph itself. The caller therefore must only:
///     (a) resize the image so its long side is 1024 (aspect ratio preserved, no
///         padding — the graph pads internally), and
///     (b) feed raw, unnormalized 0-255 RGB pixel values in HWC layout, no batch dim.
///   This matches samexporter's own inference.py/sam_onnx.py reference implementation.
/// - Encoder output "image_embeddings" is already [1, 256, 64, 64] — no reshape needed,
///   matches the decoder's expected embedding input directly.
/// - Decoder inputs/outputs match the standard 6-input SAM contract exactly
///   (image_embeddings, point_coords, point_labels, mask_input, has_mask_input,
///   orig_im_size -&gt; masks/iou_predictions/low_res_masks), so Decode() below is
///   unchanged from the brief's reference code. Point coordinates are still scaled by
///   the same long-side-1024 ratio used for the image (SAM's standard convention),
///   since the decoder has no visibility into the original photo's pixel grid.
/// - The exported "masks" output is already upscaled to the original photo resolution
///   by this decoder (it consumes orig_im_size directly), so the low-res-mask bilinear
///   upscale path in Decode() is untested against this specific export but is kept as a
///   defensive fallback for other SAM ONNX exports that only return 256x256 logits.
///
/// InferenceSession.Run is thread-safe; register this class as a singleton.
/// </summary>
public class SamOnnxModel : ISamModel, IDisposable
{
    private const int InputSize = 1024;

    private readonly InferenceSession? _encoder;
    private readonly InferenceSession? _decoder;

    public bool IsAvailable => _encoder is not null && _decoder is not null;

    public SamOnnxModel(string encoderPath, string decoderPath)
    {
        if (File.Exists(encoderPath) && File.Exists(decoderPath))
        {
            _encoder = new InferenceSession(encoderPath);
            _decoder = new InferenceSession(decoderPath);
        }
    }

    public SamEmbedding Encode(Image<Rgb24> image)
    {
        if (_encoder is null) throw new InvalidOperationException("Encoder model not loaded.");

        var origW = image.Width;
        var origH = image.Height;
        var scale = InputSize / (float)Math.Max(origW, origH);
        var scaledW = (int)Math.Round(origW * scale);
        var scaledH = (int)Math.Round(origH * scale);

        // This export bakes normalization + 1024x1024 padding into the graph itself
        // (samexporter --use-preprocess); the caller only resizes (aspect-preserving,
        // no padding) and feeds raw, unnormalized HWC pixel values — no batch dim.
        using var resized = image.Clone(ctx => ctx.Resize(scaledW, scaledH));
        var tensor = new DenseTensor<float>(new[] { scaledH, scaledW, 3 });
        resized.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    tensor[y, x, 0] = row[x].R;
                    tensor[y, x, 1] = row[x].G;
                    tensor[y, x, 2] = row[x].B;
                }
            }
        });

        var inputName = _encoder.InputMetadata.Keys.First();
        using var results = _encoder.Run(new[] { NamedOnnxValue.CreateFromTensor(inputName, tensor) });
        var embedding = results.First().AsEnumerable<float>().ToArray();
        return new SamEmbedding(embedding, scale, origW, origH);
    }

    public float[,] Decode(SamEmbedding embedding, IReadOnlyList<SamPoint> points)
    {
        if (_decoder is null) throw new InvalidOperationException("Decoder model not loaded.");

        var n = points.Count + 1; // + padding point
        var coords = new DenseTensor<float>(new[] { 1, n, 2 });
        var labels = new DenseTensor<float>(new[] { 1, n });
        for (var i = 0; i < points.Count; i++)
        {
            coords[0, i, 0] = points[i].X * embedding.Scale;
            coords[0, i, 1] = points[i].Y * embedding.Scale;
            labels[0, i] = points[i].Label;
        }
        coords[0, n - 1, 0] = 0f;
        coords[0, n - 1, 1] = 0f;
        labels[0, n - 1] = -1f;

        var embeddingTensor = new DenseTensor<float>(embedding.Data, new[] { 1, 256, 64, 64 });
        var maskInput = new DenseTensor<float>(new[] { 1, 1, 256, 256 });
        var hasMask = new DenseTensor<float>(new[] { 0f }, new[] { 1 });
        var origSize = new DenseTensor<float>(
            new[] { (float)embedding.OrigHeight, embedding.OrigWidth }, new[] { 2 });

        var candidates = new Dictionary<string, NamedOnnxValue>
        {
            ["image_embeddings"] = NamedOnnxValue.CreateFromTensor("image_embeddings", embeddingTensor),
            ["point_coords"] = NamedOnnxValue.CreateFromTensor("point_coords", coords),
            ["point_labels"] = NamedOnnxValue.CreateFromTensor("point_labels", labels),
            ["mask_input"] = NamedOnnxValue.CreateFromTensor("mask_input", maskInput),
            ["has_mask_input"] = NamedOnnxValue.CreateFromTensor("has_mask_input", hasMask),
            ["orig_im_size"] = NamedOnnxValue.CreateFromTensor("orig_im_size", origSize)
        };
        // Feed only the inputs this particular export declares.
        var inputs = _decoder.InputMetadata.Keys
            .Where(candidates.ContainsKey)
            .Select(k => candidates[k])
            .ToList();

        using var results = _decoder.Run(inputs);
        var masks = results.First(r => r.Name is "masks" or "low_res_masks");
        var tensor = (DenseTensor<float>)masks.AsTensor<float>();
        var dims = tensor.Dimensions.ToArray();
        var h = dims[^2];
        var w = dims[^1];

        if (h == embedding.OrigHeight && w == embedding.OrigWidth)
        {
            var mask = new float[h, w];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                    mask[y, x] = tensor[0, 0, y, x];
            return mask;
        }

        // Export returned low-res (e.g., 256x256) logits — upscale bilinearly to photo size.
        return BilinearResize(tensor, h, w, embedding.OrigHeight, embedding.OrigWidth,
            embedding.Scale, embedding.OrigWidth, embedding.OrigHeight);
    }

    private static float[,] BilinearResize(DenseTensor<float> src, int srcH, int srcW,
        int dstH, int dstW, float scale, int origW, int origH)
    {
        // Low-res SAM masks correspond to the 1024x1024 padded frame; only the
        // (origW*scale) x (origH*scale) top-left region is valid.
        var validW = origW * scale / 1024f * srcW;
        var validH = origH * scale / 1024f * srcH;
        var result = new float[dstH, dstW];
        for (var y = 0; y < dstH; y++)
        {
            var sy = Math.Clamp(y / (float)dstH * validH, 0, srcH - 1.001f);
            int y0 = (int)sy; var fy = sy - y0;
            for (var x = 0; x < dstW; x++)
            {
                var sx = Math.Clamp(x / (float)dstW * validW, 0, srcW - 1.001f);
                int x0 = (int)sx; var fx = sx - x0;
                var top = src[0, 0, y0, x0] * (1 - fx) + src[0, 0, y0, x0 + 1] * fx;
                var bottom = src[0, 0, y0 + 1, x0] * (1 - fx) + src[0, 0, y0 + 1, x0 + 1] * fx;
                result[y, x] = top * (1 - fy) + bottom * fy;
            }
        }
        return result;
    }

    public void Dispose()
    {
        _encoder?.Dispose();
        _decoder?.Dispose();
        GC.SuppressFinalize(this);
    }
}
