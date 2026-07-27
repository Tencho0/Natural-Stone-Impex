# Final review fix report — feature/visualizer

Fixes applied for the six findings raised in the pre-merge whole-branch review.

## 1 (Critical) — Decode-before-validation + failed attempts are quota-free

`src/NaturalStoneImpex.Api/Services/Segmentation/SegmentationService.cs`

- `SegmentNewAsync` (lines 42-121): added `var info = await Image.IdentifyAsync(photo);` (line 66)
  inside the existing try/catch, before `Image.LoadAsync<Rgb24>`. If
  `Math.Max(info.Width, info.Height) > 2 * _options.MaxImageDimension` (line 67), a Failed
  quota row is recorded (line 69) and the method returns 400 `ErrorBadImage`.
- Stream reset: `photo.Position = 0;` (line 75) before `Image.LoadAsync<Rgb24>` (line 78).
- Added `private async Task RecordAsync(string ipHash, VisualizationStatus status, long durationMs)`
  (lines 161-171) — inserts a `VisualizationRequest` row and calls `SaveChangesAsync`.
  Wired into all three failure/success points in `SegmentNewAsync`:
  - identify/load failure → `RecordAsync(..., Failed, ...)` (line 82)
  - oversized declared dimensions → `RecordAsync(..., Failed, ...)` (line 69)
  - final outcome (success or `BuildOutcome`'s no-surface 400) → `RecordAsync(..., status, ...)`
    (line 117), where `status` is derived from `outcome.StatusCode == 200`.
- The gate-busy 503 path (`ErrorBusy`) and the disabled/unavailable 503 and the two quota 429s
  intentionally do NOT record — quota checks stay first and unaffected; a quota-blocked request
  still records nothing, matching the contract.
- `RefineAsync` (lines 123-138) still records nothing — unchanged in that respect.

Covering tests (`tests/NaturalStoneImpex.Api.Tests/SegmentationServiceTests.cs`):
- `Oversized_declared_dimensions_returns_400_and_persists_failed_row` — 100x100 JPEG with
  `MaxImageDimension = 20` (100 > 2*20) → 400 + single Failed row.
- `Invalid_image_persists_failed_row_and_exhausts_per_ip_quota` — 2 bad uploads with
  `PerIpDailyLimit = 2` each persist a Failed row, 3rd request is blocked with 429.

## 2 (Important) — Unthrottled refine + unbounded point count

`src/NaturalStoneImpex.Api/Controllers/VisualizerController.cs`

- Added `ErrorTooManyPoints` constant and `MaxPoints = 50` (lines 14-15).
- `Segment` (multipart): after parsing, `if (parsed.Count > MaxPoints)` → 400 `ErrorTooManyPoints`
  (lines 51-52), parse-first-then-count per spec.
- `Refine` ([FromBody]): `if (points.Count > MaxPoints)` → same 400 (lines 65-66).

`src/NaturalStoneImpex.Api/Services/Segmentation/SegmentationService.cs`

- `RefineAsync` (lines 123-138): added a per-token refine ceiling using the existing `_cache`,
  key `$"viz-refines:{token}"` via `RefineCountCacheKey` (line 187). `IncrementRefineCount`
  (lines 173-183) reads-then-sets the counter with `Size = 1` and the same
  `SlidingExpiration` as the embedding cache entry. When the count exceeds
  `MaxRefinesPerToken = 200` (line 24), returns 429 with `ErrorQuota` (lines 133-135).

Covering tests:
- `VisualizerControllerTests.Segment_with_more_than_50_points_returns_400`
- `VisualizerControllerTests.Refine_with_more_than_50_points_returns_400`
- `SegmentationServiceTests.Refine_beyond_ceiling_returns_429` — loops 200 successful refines
  (asserting 200 each time), then asserts the 201st call returns 429.

## 3 (Important) — Admin form breaks enable+texture-in-one-save

`src/NaturalStoneImpex.Client/Pages/Admin/ProductForm.razor`, `HandleSave` (lines 389-490)

- Edit-mode branch reordered: texture upload (if `_selectedTexture is not null`) now runs
  FIRST (lines 409-420), before `UpdateAsync` (line 436). On upload error, `_errorMessage` is
  set and the save aborts (existing error display, lines 20-26 render it). On success,
  `_existingTexturePath` is refreshed via a new helper `BuildTexturePreviewDataUrlAsync`
  (lines 501-509) since `UploadTextureAsync` only returns an error, not the stored path.
- `UpdateAsync` now runs after the texture upload, using the already-known `productId`.
- Create-mode branch is untouched (texture upload only applies to edit mode, as before).
- The image-upload block (lines 469-479) keeps its original code and position (right after
  the create/update branch, before `Navigation.NavigateTo`) — untouched per the constraint.

No new automated test — this is a Blazor component interaction; verified by re-reading the
reordered `HandleSave` method against the acceptance criteria (texture-then-update, abort on
texture error, image wiring unchanged).

## 4 (Important) — Unhandled product-load failure on the visualizer page

`src/NaturalStoneImpex.Client/Pages/Public/Visualizer.razor`, `OnInitializedAsync`
(lines 189-206)

- Wrapped `_products = await VisualizerService.GetProductsAsync();` in
  `try { ... } catch (HttpRequestException) { _products = new List<VisualizerProductDto>(); }`
  (lines 191-198). On failure the page now renders the existing
  "Визуализаторът не е наличен в момента." empty state (line 24) instead of crashing.

## 5 (Minor) — Wire dead config `MaxUploadBytes`

`src/NaturalStoneImpex.Api/Controllers/VisualizerController.cs`

- Injected `IOptions<VisualizerOptions> options` via the constructor (lines 23-29), stored as
  `_options`.
- `Segment` now rejects `photo.Length > _options.MaxUploadBytes` with 400
  `{ "error": "Моля, качете снимка във формат JPG или PNG до 10 MB." }` (lines 45-46), before
  parsing points or calling the service. `[RequestSizeLimit(12_000_000)]` (line 39) kept as
  the outer bound.

Covering test:
- `VisualizerControllerTests.Segment_with_oversized_photo_length_returns_400` — `FormFile` with
  a 1-byte backing stream but `length: 11_000_000` passed to the ctor.

## 6 (Minor) — Perspective handle hit area

`src/NaturalStoneImpex.Client/Pages/Public/Visualizer.razor` (SVG block, around line 88-97)

- For each of the 4 corners, an invisible hit circle is rendered before (painted below) the
  visible handle: same `cx`/`cy`, `r="@Inv(_photoW * 0.055)"`, `class="viz-handle-hit"`, same
  `@onpointerdown` handler with `preventDefault`/`stopPropagation` (line 92 area). The visible
  `.viz-handle` circle (r = 0.02 * photoW) is unchanged.

`src/NaturalStoneImpex.Client/wwwroot/css/app.css` (line 114-118): added
```css
.viz-handle-hit {
    fill: transparent;
    pointer-events: all;
    cursor: grab;
}
```

Verified with the headless Edge harness (visualizer.js untouched, so this was a sanity check
on the page-independent engine, not a targeted test of the new markup) — result: `ALL PASS`.

## Test run

```
dotnet build         → Build succeeded, 0 Warning(s), 0 Error(s)
dotnet test          → Passed! Failed: 0, Passed: 25, Skipped: 0, Total: 25
```

25 = 19 pre-existing + 6 new (3 in `SegmentationServiceTests`, 3 in `VisualizerControllerTests`).

Headless harness:
```
msedge.exe --headless=new --disable-gpu-sandbox --virtual-time-budget=5000 --dump-dom
  tests/manual/visualizer-harness.html
→ <p id="status" class="pass">ALL PASS — ...</p>
```

## Deviations from the brief

- None functionally. One implementation note: `UploadTextureAsync` on the client only
  returns `string? error`, not the stored path, so "update `_existingTexturePath`" (finding 3)
  is implemented via a local base64 data-URL preview built from the just-uploaded
  `IBrowserFile` (mirrors the existing pattern already used for the product image preview in
  `OnImageSelected`), rather than a path returned from the server.
