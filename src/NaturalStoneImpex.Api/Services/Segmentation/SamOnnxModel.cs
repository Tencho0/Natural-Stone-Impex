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
///   orig_im_size -&gt; masks/iou_predictions/low_res_masks). Point coordinates are scaled
///   by the same long-side-1024 ratio used for the image (SAM's standard convention),
///   since the decoder has no visibility into the original photo's pixel grid.
/// - Decode() deliberately IGNORES the decoder's full-resolution "masks" output and
///   upsamples "low_res_masks" itself. In this export the in-graph postprocess that turns
///   the low-res logits into "masks" (using orig_im_size) is geometrically misregistered:
///   it stretches Y by roughly 1.5x(H/W), and X as well for portrait photos. E.g. an object
///   at y 60-80% of a 1920x1080 photo came back at y 51-68%, and at y 90-100% on a
///   1024x1024 photo. "low_res_masks" ([1,1,256,256] logits over the padded 1024x1024
///   frame, photo content in its top-left W*scale x H*scale region) is correctly
///   registered for every aspect ratio — see UpsampleLowResLogits and the registration
///   tests in SamOnnxModelTests.
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
        // Never read "masks" — it is misregistered in this export (see class remarks).
        var lowRes = (DenseTensor<float>)results.First(r => r.Name == "low_res_masks").AsTensor<float>();
        var lowSize = lowRes.Dimensions[^1];
        // Single-mask export: the first lowSize^2 values are the [0, 0] mask.
        return UpsampleLowResLogits(lowRes.Buffer.Span[..(lowSize * lowSize)], lowSize,
            embedding.OrigWidth, embedding.OrigHeight, embedding.Scale);
    }

    /// <summary>
    /// Bilinearly upsamples SAM's square low-res mask logits to the original photo grid.
    /// The low-res grid (lowSize x lowSize, normally 256) covers the padded 1024x1024 encoder
    /// frame; the photo occupies its top-left (origW*scale) x (origH*scale) region, where
    /// scale = 1024 / max(origW, origH). Sampling is pixel-centre aligned, matching SAM's own
    /// upscale → crop → resize postprocess (bilinear, align_corners=false).
    /// </summary>
    /// <param name="low">Row-major lowSize*lowSize logits of a single mask.</param>
    /// <returns>Logits [origH, origW]; values &gt; 0 are selected.</returns>
    public static float[,] UpsampleLowResLogits(ReadOnlySpan<float> low, int lowSize,
        int origW, int origH, float scale)
    {
        if (low.Length < lowSize * lowSize)
            throw new ArgumentException("Low-res buffer is smaller than lowSize^2.", nameof(low));

        var k = lowSize / (float)InputSize * scale; // original px -> low-res px
        var maxCoord = lowSize - 1.001f;           // keeps index + 1 in range

        // Horizontal sample positions are the same for every row — compute them once.
        var x0s = new int[origW];
        var fxs = new float[origW];
        for (var x = 0; x < origW; x++)
        {
            var sx = Math.Clamp((x + 0.5f) * k - 0.5f, 0f, maxCoord);
            x0s[x] = (int)sx;
            fxs[x] = sx - x0s[x];
        }

        var result = new float[origH, origW];
        for (var y = 0; y < origH; y++)
        {
            var sy = Math.Clamp((y + 0.5f) * k - 0.5f, 0f, maxCoord);
            var y0 = (int)sy;
            var fy = sy - y0;
            var top = low.Slice(y0 * lowSize, lowSize);
            var bottom = low.Slice((y0 + 1) * lowSize, lowSize);
            for (var x = 0; x < origW; x++)
            {
                var x0 = x0s[x];
                var fx = fxs[x];
                var t = top[x0] + (top[x0 + 1] - top[x0]) * fx;
                var b = bottom[x0] + (bottom[x0 + 1] - bottom[x0]) * fx;
                result[y, x] = t + (b - t) * fy;
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
