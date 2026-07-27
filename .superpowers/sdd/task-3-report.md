# Task 3 Report: ONNX Runtime + MobileSAM Model Wrapper

## Summary

Added ONNX Runtime + ImageSharp to the API project, downloaded real MobileSAM ONNX
encoder/decoder models, and implemented `ISamModel`/`SamOnnxModel` behind the exact
signatures specified in the task brief. The integration test runs against the real
downloaded models (not skipped) and passes.

This was the highest-risk task in the plan, and two real deviations from the brief
surfaced during execution — both documented below and in code comments.

## Model Source

Hugging Face repo `vietanhdev/segment-anything-onnx-models` (samexporter project,
Apache-2.0). Unlike the brief's assumption of individual file downloads, this repo
ships one zip per model variant — there are no loose `.onnx` files at the repo root.

- Zip: `https://huggingface.co/vietanhdev/segment-anything-onnx-models/resolve/main/mobile_sam_20230629.zip` (36,655,105 bytes / ~36.7 MB)
- Verified via the zip's own `config.yaml`:
  ```
  encoder_model_path: mobile_sam.encoder.onnx
  decoder_model_path: sam_vit_h_4b8939.decoder.onnx
  ```
- Extracted and renamed to the stable app-facing names:
  - `src/NaturalStoneImpex.Api/MLModels/mobilesam-encoder.onnx` — 28,157,093 bytes (~28.2 MB)
  - `src/NaturalStoneImpex.Api/MLModels/mobilesam-decoder.onnx` — 16,500,272 bytes (~16.5 MB)

The decoder is named after ViT-H because the SAM mask decoder is a small,
backbone-agnostic network reused across all SAM/MobileSAM exports in this repo — this
is expected (confirmed via the zip's `config.yaml` pairing), not a mistake.

`scripts/download-visualizer-models.ps1` downloads the zip to a temp file, extracts
just the two needed entries with `Expand-Archive`, copies+renames them into
`MLModels/`, and cleans up the temp zip/extract folder. Ran successfully end-to-end
(re-verified after the manual first download): `powershell -File scripts/download-visualizer-models.ps1`.

## Tensor-Contract Deviations (adapted, documented at the top of `SamOnnxModel.cs`)

Printed real `InputMetadata`/`OutputMetadata` via a scratch test (written, run, then
deleted — not part of the final diff):

```
=== ENCODER INPUTS ===
input_image: System.Single [-1,-1,3]
=== ENCODER OUTPUTS ===
image_embeddings: System.Single [1,256,64,64]
=== DECODER INPUTS ===
image_embeddings: System.Single [1,256,64,64]
point_coords: System.Single [1,-1,2]
point_labels: System.Single [1,-1]
mask_input: System.Single [1,1,256,256]
has_mask_input: System.Single [1]
orig_im_size: System.Single [2]
=== DECODER OUTPUTS ===
masks: System.Single [-1,-1,-1,-1]
iou_predictions: System.Single [-1,1]
low_res_masks: System.Single [-1,1,-1,-1]
```

1. **Encoder input is NOT `[1,3,1024,1024]` normalized CHW.** It's `input_image`,
   float32, rank-3 HWC `[-1,-1,3]` (dynamic H/W, no batch dim). This export was built
   with samexporter's `--use-preprocess` flag, which bakes ImageNet mean/std
   normalization and 1024x1024 padding into the ONNX graph itself. Confirmed against
   samexporter's own `sam_onnx.py` reference implementation (fetched from GitHub):
   the caller resizes the image so the long side is 1024 (aspect-preserving, **no**
   padding) and feeds raw unnormalized 0-255 RGB pixel values in HWC layout.
   `Encode()` was rewritten accordingly — removed the `[1,3,1024,1024]` tensor
   allocation, the ImageNet `Mean`/`Std` constants, and the per-channel
   normalization; now builds a `[scaledH, scaledW, 3]` tensor of raw pixel values.
   Running the encoder with the wrapper's original (brief) code failed with:
   `OnnxRuntimeException: Invalid rank for input: input_image Got: 4 Expected: 3`
   — this was the RED signal that triggered the investigation.
2. **Encoder output already `[1,256,64,64]`** — matches the decoder's expected
   embedding shape directly, no reshape needed.
3. **Decoder contract matches the brief's assumption exactly** (all 6 standard SAM
   inputs present, `masks` output already upscaled to original photo resolution via
   `orig_im_size`) — `Decode()` is unchanged from the brief's reference code. The
   low-res bilinear-upscale fallback path is kept for other exports but is untested
   against this specific model since `masks` already comes back at full resolution.

## Unplanned Blocker: SixLabors.ImageSharp.Drawing requires a paid license

The brief's test used `SixLabors.ImageSharp.Drawing` (`RectangularPolygon` + `Fill`)
to paint the synthetic "driveway" rectangle. Adding that package (pinned to `3.0.0`
to match the core `ImageSharp` v3 line already in use) broke the build with:

```
error : No Six Labors license found. Set $(SixLaborsLicenseKey), set $(SixLaborsLicenseFile),
         or add a 'sixlabors.lic' file to the project/workspace.
error : Please obtain a license from https://sixlabors.com/pricing/
```

Inspected the package's `.targets` file — it runs a `SixLabors.Licensing.ValidateLicenseTask`
`BeforeTargets="CoreCompile"` unconditionally unless a license key/file is present. This
is a genuine commercial-licensing wall introduced in `ImageSharp.Drawing` 3.x (core
`SixLabors.ImageSharp` 3.1.12 has no such build target — verified no `.targets` file
ships with it, only a `.props`). Acquiring a Six Labors license (even the free/nonprofit
tiers) requires an application/signup process, which isn't appropriate to do
unilaterally inside an automated coding task.

**Resolution:** removed the `SixLabors.ImageSharp.Drawing` package reference entirely
and rewrote the test's rectangle-fill using only core `ImageSharp` pixel access
(`ProcessPixelRows`), producing an identical synthetic photo (gray 600x300 rectangle at
(300,400) on a green background) without any licensed dependency. This is a test-only
change; it does not touch `ISamModel`/`SamOnnxModel`. Documented inline in
`SamOnnxModelTests.cs`.

## TDD Evidence

### RED — Step 4 (brief's exact command)

```
dotnet test tests/NaturalStoneImpex.Api.Tests --filter SamOnnxModelTests
```

```
error CS0234: The type or namespace name 'Segmentation' does not exist in the
namespace 'NaturalStoneImpex.Api.Services' (are you missing an assembly reference?)
```

(`ISamModel`/`SamOnnxModel`/`SamPoint` did not exist yet, as expected.)

### Intermediate RED — real contract mismatch (before adapting `Encode()`)

```
dotnet test tests/NaturalStoneImpex.Api.Tests --filter SamOnnxModelTests
```

```
Microsoft.ML.OnnxRuntime.OnnxRuntimeException : [ErrorCode:InvalidArgument]
Invalid rank for input: input_image Got: 4 Expected: 3
Please fix either the inputs/outputs or the model.
```

### GREEN — Step 6, after adapting `Encode()` to the real HWC contract

```
dotnet test tests/NaturalStoneImpex.Api.Tests --filter SamOnnxModelTests
```

```
Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 2 s
```

Ran with `--logger "console;verbosity=detailed"` to confirm the real, non-skipped
assertion path executed:

```
[xUnit.net 00:00:00.10]   Starting:    NaturalStoneImpex.Api.Tests
[xUnit.net 00:00:01.89]   Finished:    NaturalStoneImpex.Api.Tests
  Passed NaturalStoneImpex.Api.Tests.SamOnnxModelTests.Encode_and_decode_segments_the_tapped_region [1 s]
Total tests: 1
     Passed: 1
```

Encoder+decoder CPU inference completed in ~1 second (models were present and loaded
— the "models not downloaded, skip" early-return branch was not taken since
`mobilesam-encoder.onnx`/`mobilesam-decoder.onnx` exist in `MLModels/`).

### Full suite (`dotnet test`, no filter)

```
Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4, Duration: 2 s
```

(4 = 1 new `SamOnnxModelTests` + 3 pre-existing tests from Tasks 1–2.)

### Full solution build

```
dotnet build
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Files Changed

- Modified: `src/NaturalStoneImpex.Api/NaturalStoneImpex.Api.csproj` — added
  `Microsoft.ML.OnnxRuntime` (1.27.1), `SixLabors.ImageSharp` (**3.1.12**, pinned
  explicitly rather than the `dotnet add`-default 4.0.0, per the brief's "ImageSharp
  v3 API" environment note), and the `Content Include="MLModels\*.onnx"` item with
  `CopyToOutputDirectory`/`CopyToPublishDirectory` set to `PreserveNewest`. Verified
  the models actually land in `bin/Debug/net8.0/MLModels/` after a plain build.
- Modified: `.gitignore` — appended
  `src/NaturalStoneImpex.Api/MLModels/*.onnx`.
- Created: `scripts/download-visualizer-models.ps1` — downloads the zip, extracts,
  renames, cleans up temp files. Re-run and verified end-to-end.
- Created: `src/NaturalStoneImpex.Api/MLModels/.gitkeep`.
- Created: `src/NaturalStoneImpex.Api/Services/Segmentation/ISamModel.cs` — exact
  signatures from the brief, unchanged.
- Created: `src/NaturalStoneImpex.Api/Services/Segmentation/SamOnnxModel.cs` —
  `Encode()` rewritten for the real HWC/no-normalization contract; `Decode()`
  unchanged from the brief; deviations documented in the class doc comment.
- Created: `tests/NaturalStoneImpex.Api.Tests/SamOnnxModelTests.cs` — brief's test
  logic and assertions unchanged; only the synthetic-image rectangle-fill mechanism
  was swapped from `SixLabors.ImageSharp.Drawing` to plain `ProcessPixelRows` (see
  blocker above), with the reason documented inline.
- Not modified (net no-op): `tests/NaturalStoneImpex.Api.Tests.csproj` — briefly
  added then removed `SixLabors.ImageSharp.Drawing` once the licensing wall was hit;
  final diff against this file is empty.

Model binaries (`MLModels/*.onnx`) are present on disk but correctly excluded from
git — verified with `git check-ignore -v` and by inspecting `git status --porcelain`
before staging.

## Self-Review

- [x] `ISamModel`/`SamPoint`/`SamEmbedding` signatures exactly match the brief
      (record fields, method signatures, `IsAvailable` property) — no deviation.
- [x] Integration test passes with REAL models, not skipped — confirmed via detailed
      logger output showing `Starting`/`Finished` around actual assertions, ~1s
      duration consistent with real CPU inference (not an instant skip-return).
      All original assertions from the brief are intact (embedding dimensions, mask
      dimensions, tapped-pixel selected, selected-fraction range 0.02–0.90).
- [x] Models gitignored (`git check-ignore` confirms); `.gitkeep` and download
      script are committed; csproj has the `Content Include` wired for both
      `CopyToOutputDirectory` and `CopyToPublishDirectory`, verified against the
      actual build output directory.
- [x] Both contract deviations (encoder HWC/no-normalization input, and the
      ImageSharp.Drawing licensing substitution) are documented — the former in the
      required location (top-of-file comment in `SamOnnxModel.cs`), the latter
      inline in `SamOnnxModelTests.cs` plus this report, since it's a test-only
      concern rather than a `SamOnnxModel` contract deviation.
- [x] Full `dotnet test` run (all 4 tests) and full `dotnet build` both clean.

## Concerns

- `SixLabors.ImageSharp` was pinned to `3.1.12` instead of accepting the
  `dotnet add`-default `4.0.0`, per the environment note that the brief's code
  targets the v3 API. Worth confirming this pin is what later visualizer tasks
  expect, since `dotnet add` would otherwise silently drift to v4 on a fresh
  restore if the version were ever left unpinned elsewhere.
- The `SixLabors.ImageSharp.Drawing` commercial-license wall may resurface if any
  later task (e.g., mask overlay rendering) reaches for that package again for
  production drawing needs (not just test fixtures) — worth flagging to whoever
  scopes that task so they don't hit the same surprise mid-implementation.
- The bilinear low-res-mask upscale fallback path in `Decode()` is not exercised by
  the real MobileSAM export used here (its `masks` output is already full
  resolution), so that branch is currently only compile-verified, not
  behavior-tested end-to-end.
