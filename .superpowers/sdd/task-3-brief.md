### Task 3: ONNX runtime + MobileSAM model wrapper

**Files:**
- Modify: `src/NaturalStoneImpex.Api/NaturalStoneImpex.Api.csproj`
- Create: `scripts/download-visualizer-models.ps1`
- Create: `src/NaturalStoneImpex.Api/MLModels/.gitkeep`
- Modify: `.gitignore` (repo root)
- Create: `src/NaturalStoneImpex.Api/Services/Segmentation/ISamModel.cs`
- Create: `src/NaturalStoneImpex.Api/Services/Segmentation/SamOnnxModel.cs`
- Test: `tests/NaturalStoneImpex.Api.Tests/SamOnnxModelTests.cs`

**Interfaces:**
- Produces (Tasks 5–6 depend on these exact signatures):

```csharp
public record SamPoint(float X, float Y, int Label);                    // Label: 1 = add, 0 = remove
public record SamEmbedding(float[] Data, float Scale, int OrigWidth, int OrigHeight);
public interface ISamModel
{
    bool IsAvailable { get; }
    SamEmbedding Encode(SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgb24> image);
    float[,] Decode(SamEmbedding embedding, IReadOnlyList<SamPoint> points); // [height, width] logits, >0 = selected
}
```

- [ ] **Step 1: Add packages and model folder**

```powershell
dotnet add src/NaturalStoneImpex.Api package Microsoft.ML.OnnxRuntime
dotnet add src/NaturalStoneImpex.Api package SixLabors.ImageSharp
New-Item -ItemType Directory -Force src/NaturalStoneImpex.Api/MLModels
New-Item -ItemType File src/NaturalStoneImpex.Api/MLModels/.gitkeep
```

Append to the repo-root `.gitignore`:

```
# Visualizer ONNX models (downloaded via scripts/download-visualizer-models.ps1)
src/NaturalStoneImpex.Api/MLModels/*.onnx
```

Also add to `src/NaturalStoneImpex.Api/NaturalStoneImpex.Api.csproj` (inside `<Project>`, after the existing `<ItemGroup>`) so models deploy with `dotnet publish`:

```xml
  <ItemGroup>
    <Content Include="MLModels\*.onnx" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" />
  </ItemGroup>
```

- [ ] **Step 2: Create the model download script**

The pre-exported MobileSAM ONNX files live in the Hugging Face repo `vietanhdev/segment-anything-onnx-models` (from the `samexporter` project, Apache-2.0). **First open https://huggingface.co/vietanhdev/segment-anything-onnx-models/tree/main in a browser and note the exact MobileSAM encoder/decoder file names** (e.g., `mobile_sam.encoder.onnx` / `mobile_sam.decoder.onnx` — names may differ or be zipped), then set them in the variables below. Create `scripts/download-visualizer-models.ps1`:

```powershell
# Downloads the MobileSAM ONNX models used by the product visualizer.
# Verify file names at https://huggingface.co/vietanhdev/segment-anything-onnx-models/tree/main
$repo = "https://huggingface.co/vietanhdev/segment-anything-onnx-models/resolve/main"
$encoderFile = "mobile_sam.encoder.onnx"   # <-- confirm against the repo listing
$decoderFile = "mobile_sam.decoder.onnx"   # <-- confirm against the repo listing
$target = Join-Path $PSScriptRoot "..\src\NaturalStoneImpex.Api\MLModels"

Invoke-WebRequest "$repo/$encoderFile" -OutFile (Join-Path $target "mobilesam-encoder.onnx")
Invoke-WebRequest "$repo/$decoderFile" -OutFile (Join-Path $target "mobilesam-decoder.onnx")
Write-Host "Models downloaded to $target"
```

Run it: `powershell -File scripts/download-visualizer-models.ps1`. Expected: two `.onnx` files in `src/NaturalStoneImpex.Api/MLModels/` (encoder tens of MB, decoder a few MB). Fallback if the repo layout changed: `pip install samexporter` and export from the MobileSAM checkpoint per https://github.com/vietanhdev/samexporter#usage, then copy the outputs to the same target names.

- [ ] **Step 3: Write the failing integration test**

Create `tests/NaturalStoneImpex.Api.Tests/SamOnnxModelTests.cs`. The test builds a synthetic photo (gray "driveway" rectangle on green background), taps its center, and expects a mask that covers the tap but not the whole image. It skips silently when model files are absent (CI without models):

```csharp
using Microsoft.Extensions.Options;
using NaturalStoneImpex.Api.Services.Segmentation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NaturalStoneImpex.Api.Tests;

public class SamOnnxModelTests
{
    private static string ModelsDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "NaturalStoneImpex.Api", "MLModels"));

    [Fact]
    public void Encode_and_decode_segments_the_tapped_region()
    {
        var encoderPath = Path.Combine(ModelsDir, "mobilesam-encoder.onnx");
        var decoderPath = Path.Combine(ModelsDir, "mobilesam-decoder.onnx");
        if (!File.Exists(encoderPath) || !File.Exists(decoderPath))
            return; // models not downloaded — skip (see scripts/download-visualizer-models.ps1)

        using var image = new Image<Rgb24>(1024, 768, new Rgb24(60, 140, 60));
        image.Mutate(ctx => ctx.Fill(new Rgb24(128, 126, 124),
            new RectangularPolygon(300, 400, 600, 300))); // gray rectangle = "driveway"

        var model = new SamOnnxModel(encoderPath, decoderPath);
        Assert.True(model.IsAvailable);

        var embedding = model.Encode(image);
        Assert.Equal(1024, embedding.OrigWidth);
        Assert.Equal(768, embedding.OrigHeight);

        var logits = model.Decode(embedding, new[] { new SamPoint(600f, 550f, 1) });
        Assert.Equal(768, logits.GetLength(0));
        Assert.Equal(1024, logits.GetLength(1));
        Assert.True(logits[550, 600] > 0, "tapped pixel must be selected");

        var selected = 0;
        for (var y = 0; y < 768; y++)
            for (var x = 0; x < 1024; x++)
                if (logits[y, x] > 0) selected++;
        var fraction = selected / (768.0 * 1024.0);
        Assert.InRange(fraction, 0.02, 0.90); // some region, not the whole image
    }
}
```

Add the drawing package the test uses: `dotnet add tests/NaturalStoneImpex.Api.Tests package SixLabors.ImageSharp.Drawing`

- [ ] **Step 4: Run test to verify it fails**

Run: `dotnet test tests/NaturalStoneImpex.Api.Tests --filter SamOnnxModelTests`
Expected: FAIL — `SamOnnxModel` / `SamPoint` do not exist.

- [ ] **Step 5: Implement the interface and wrapper**

Create `src/NaturalStoneImpex.Api/Services/Segmentation/ISamModel.cs`:

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NaturalStoneImpex.Api.Services.Segmentation;

/// <summary>A point prompt in original-photo pixel coordinates. Label: 1 = include, 0 = exclude.</summary>
public record SamPoint(float X, float Y, int Label);

/// <summary>Encoder output plus the geometry needed to run the decoder. ~4 MB per photo.</summary>
public record SamEmbedding(float[] Data, float Scale, int OrigWidth, int OrigHeight);

public interface ISamModel
{
    bool IsAvailable { get; }
    SamEmbedding Encode(Image<Rgb24> image);
    /// <returns>Mask logits [OrigHeight, OrigWidth]; values &gt; 0 are selected.</returns>
    float[,] Decode(SamEmbedding embedding, IReadOnlyList<SamPoint> points);
}
```

Create `src/NaturalStoneImpex.Api/Services/Segmentation/SamOnnxModel.cs`:

```csharp
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NaturalStoneImpex.Api.Services.Segmentation;

/// <summary>
/// MobileSAM via ONNX Runtime on CPU. Standard SAM contract: images are resized so the
/// long side is 1024, padded bottom/right to 1024x1024, normalized with ImageNet mean/std.
/// Point prompts are given in the 1024-scale space; an extra (0,0) point with label -1
/// is appended as the "no box" padding prompt required by SAM decoders.
/// InferenceSession.Run is thread-safe; register this class as a singleton.
/// </summary>
public class SamOnnxModel : ISamModel, IDisposable
{
    private const int InputSize = 1024;
    private static readonly float[] Mean = { 123.675f, 116.28f, 103.53f };
    private static readonly float[] Std = { 58.395f, 57.12f, 57.375f };

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

        using var resized = image.Clone(ctx => ctx.Resize(scaledW, scaledH));
        var tensor = new DenseTensor<float>(new[] { 1, 3, InputSize, InputSize });
        resized.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    tensor[0, 0, y, x] = (row[x].R - Mean[0]) / Std[0];
                    tensor[0, 1, y, x] = (row[x].G - Mean[1]) / Std[1];
                    tensor[0, 2, y, x] = (row[x].B - Mean[2]) / Std[2];
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
```

- [ ] **Step 6: Run the test; inspect the model contract if it fails**

Run: `dotnet test tests/NaturalStoneImpex.Api.Tests --filter SamOnnxModelTests`
Expected: PASS (takes a few seconds — CPU encoding).
If it fails on tensor names/shapes, print the actual contract and adapt `Encode`/`Decode` input/output names to it:

```csharp
// temporary diagnostic inside the test:
foreach (var kv in model /* expose sessions temporarily */) { }
// or simpler: new InferenceSession(path).InputMetadata / .OutputMetadata in a scratch test,
// printing kv.Key, kv.Value.ElementType, string.Join(",", kv.Value.Dimensions)
```

Document any deviation in a comment at the top of `SamOnnxModel.cs`.

- [ ] **Step 7: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): MobileSAM ONNX wrapper with CPU inference"
```

---

