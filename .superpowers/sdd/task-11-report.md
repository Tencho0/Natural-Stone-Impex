# Task 11 Report — Product panel, editing toolbar, perspective handles, compare, actions

## What was implemented

Per `.superpowers/sdd/task-11-brief.md`, all four steps:

1. **JS helper** (`src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js`): added `api.getStageRect()` next to `setCompareRatio`/before `exportResultDataUrl`, verbatim from the brief — returns `{ left, top, width, height }` from `photoImg.getBoundingClientRect()`.
2. **New component** `src/NaturalStoneImpex.Client/Components/VisualizerProductPanel.razor`, created verbatim from the brief: search box (`_search`, case-insensitive `Contains`), category `<select>` built from `Products.Select(p => new { p.CategoryId, p.CategoryName }).Distinct()`, product list with active-state highlighting (`product.Id == SelectedId ? "active" : ""`), `Disabled` parameter applied to each button, and the Bulgarian empty-state message when `Filtered` is empty.
3. **Extended** `src/NaturalStoneImpex.Client/Pages/Public/Visualizer.razor`:
   - Added `@inject CartService CartService` after the existing `@inject` lines.
   - Replaced the single-column workspace markup with the brief's two-column `row g-3` layout (`col-lg-8` editing surface + `col-lg-4` `<VisualizerProductPanel>`), including the editing toolbar (mode buttons, Изчисти, Перспектива toggle, brush-size slider), the perspective-handle SVG overlay, the three post-mask sliders (Размер на камъка / Завъртане / Преди-След), and the action buttons (Изтегли изображението / Добави в количката / Виж продукта) with the cart-confirmation alert.
   - Added all new `@code` state (`_mode`, `_brushSize`, `_showHandles`, `_corners`, `_photoW`/`_photoH`, `_dragIndex`, `_scale`, `_rotation`, `_compare`, `_cartMessage`) and methods (`ModeButton`, `HandlePolygon`, `SetModeAsync`, `OnBrushSizeChanged`, `ClearMaskAsync`, `ToggleHandlesAsync`, `OnHandleDown/Move/Up`, `OnScaleChanged`, `OnRotationChanged`, `OnCompareChanged`, `OnProductSelectedAsync`, `DownloadAsync`, `AddToCart`, plus `StageRect`/`PhotoSize` records) — all copied verbatim from the brief.
   - `InitStageAsync` now captures the JS `loadPhotoFromDataUrl` return value as `PhotoSize` and wires `_photoW`/`_photoH` (brief Step 3.4).
   - `ResetAsync` now also resets `_mode`, `_showHandles`, `_corners`, `_scale`, `_rotation`, `_compare`, `_cartMessage` (brief Step 3.5).
4. **CSS**: appended `.viz-handles`, `.viz-grid-outline`, `.viz-grid-line`, `.viz-handle`, `.viz-product-list`, `.viz-product-thumb` to `src/NaturalStoneImpex.Client/wwwroot/css/app.css`, verbatim from the brief.

## Integration with Task 10's three approved fixes — all preserved verbatim, untouched

- **(a) Manual query parser** (`ParseProductIdFromQuery` via `new Uri(uri).Query`): untouched — Task 11 doesn't touch `OnInitializedAsync` or the parser.
- **(b) `catch (JSException)` in `OnCanvasTapAsync`**: untouched, still wraps the segment/refine/setMaskPng/corners/`ApplySelectedProductAsync` block, still sets the Bulgarian error string and clears `_busy` in `finally`.
- **(c) `OnPhotoSelectedAsync`'s restructured error scopes with `InitStageAsync` in its own try/catch**: untouched structurally — the only change inside `InitStageAsync` is the `loadPhotoFromDataUrl` call now returning/deserializing a `PhotoSize` instead of being invoked as `InvokeAsync<object>` and discarded. The outer `try/catch (JSException)` around `await InitStageAsync()` in `OnPhotoSelectedAsync` is unchanged, so a JSException thrown by any step inside `InitStageAsync` (including the now-typed `loadPhotoFromDataUrl` call) is still caught there and surfaces the same Bulgarian error message.

## Verification evidence

**Harness re-run** (`tests/manual/visualizer-harness.html`), both paths, via headless Edge — confirms the new `getStageRect` addition didn't break anything (the harness doesn't exercise `getStageRect` itself, per the task's own note):

```
WebGL path:
<title>visualizer.js harness (webgl)</title>
<p id="status" class="pass">ALL PASS — now verify visually: stones must recede with perspective, shadow band must remain visible on the stones.</p>

Fallback path (?fallback=1):
<title>visualizer.js harness (canvas-2d)</title>
<p id="status" class="pass">ALL PASS — now verify visually: stones must recede with perspective, shadow band must remain visible on the stones.</p>
```

(Note: on this Windows/Git-Bash environment, the repo path contains a space — `GitHub -Tencho Bostandzhiev` — so the `file://` URL had to be built from `pwd -W` with `%20` substituted for the space; a plain POSIX `file://$(pwd)/...` URL 404'd in Edge. Not a code issue, just an environment quirk for running the harness.)

**Build** (`dotnet build`, whole solution):
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:14.37
```

**Tests** (`dotnet test`):
```
Passed!  - Failed: 0, Passed: 19, Skipped: 0, Total: 19, Duration: 3 s
```

**Live manual verification (brief Step 5) was NOT run** — no reachable SQL Server for the API in this environment, matching the pattern established in Tasks 6/9/10. Deferred to Task 14's E2E checklist.

## Interop cross-check (new/changed calls only — Task 10's calls were already verified in the Task 10 report and are unchanged)

| Page call | JS signature | Match |
|---|---|---|
| `JS.InvokeAsync<PhotoSize>("nsiVisualizer.loadPhotoFromDataUrl", dataUrl)` | `loadPhotoFromDataUrl` resolves Promise `{width, height}` | OK — case-insensitive JSON property matching (same pattern already used for `defaultCornersFromMask`/`double[]`, and matches Blazor's `JSRuntime` default `PropertyNameCaseInsensitive = true`) |
| `JS.InvokeVoidAsync("nsiVisualizer.setMode", mode)` | `api.setMode = function (m)` | OK |
| `JS.InvokeVoidAsync("nsiVisualizer.setBrushSize", _brushSize)` | `api.setBrushSize = function (px)` | OK |
| `JS.InvokeVoidAsync("nsiVisualizer.clearMask")` | `clearMask: function ()` | OK |
| `JS.InvokeAsync<double[]>("nsiVisualizer.defaultCornersFromMask")` | `defaultCornersFromMask: function ()` → plain array | OK (same pattern already verified in Task 10) |
| `JS.InvokeAsync<StageRect>("nsiVisualizer.getStageRect")` | new `getStageRect: function ()` → `{left, top, width, height}` | OK — matches `StageRect(double Left, double Top, double Width, double Height)` via case-insensitive property binding |
| `JS.InvokeVoidAsync("nsiVisualizer.setCorners", (object)_corners)` | `setCorners: function (c)` | OK |
| `JS.InvokeVoidAsync("nsiVisualizer.render")` | `render: function ()` | OK |
| `JS.InvokeVoidAsync("nsiVisualizer.setScale", _scale)` | `setScale: function (f)` | OK |
| `JS.InvokeVoidAsync("nsiVisualizer.setRotation", _rotation)` | `setRotation: function (deg)` | OK |
| `JS.InvokeVoidAsync("nsiVisualizer.setCompareRatio", _compare)` | `setCompareRatio: function (percent)` | OK |
| `JS.InvokeVoidAsync("nsiVisualizer.downloadResult", "vizualizacia.jpg")` | `downloadResult: function (filename)` | OK |

No mismatches — every name and arity checked against the actual `visualizer.js` source (read in full both before and after the edit).

## Files changed

- `src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js` (+`getStageRect`)
- `src/NaturalStoneImpex.Client/Components/VisualizerProductPanel.razor` (new)
- `src/NaturalStoneImpex.Client/Pages/Public/Visualizer.razor` (workspace markup + `@code` extended)
- `src/NaturalStoneImpex.Client/wwwroot/css/app.css` (appended 6 rules)

Commit: `0393eb2` — "feat(visualizer): product panel, mask tools, perspective handles, compare and actions" (4 files, 317 insertions, 26 deletions).

## Self-review checklist

- **Task-10 fixes preserved?** Yes — verified above, all three untouched.
- **Every interop call name/arity exists in `visualizer.js`?** Yes — cross-checked table above; no new JS surface needed beyond `getStageRect`.
- **`PhotoSize` record + `_photoW`/`_photoH` wiring?** Yes, per brief Step 3.4, inside `InitStageAsync`.
- **Handle drag math**: photo-px conversion via `getStageRect`, `Math.Clamp` to `[0, _photoW]`/`[0, _photoH]`, then `setCorners` + `render` — implemented verbatim from the brief.
- **Panel filter**: category dropdown built from `Distinct()` over `{CategoryId, CategoryName}`, search via case-insensitive `Contains`, active state via `product.Id == SelectedId`, `Disabled` parameter wired to every list button — all verbatim.
- **`ResetAsync` resets everything new?** Yes — `_mode`, `_showHandles`, `_corners`, `_scale`, `_rotation`, `_compare`, `_cartMessage` all reset, in addition to the pre-existing fields.
- **Product switching doesn't call the server?** Confirmed — `OnProductSelectedAsync` → `ApplySelectedProductAsync` only calls `nsiVisualizer.setProductTexture` + `nsiVisualizer.render`; no `IVisualizerService` call.
- **Bulgarian strings byte-exact vs. brief?** Yes, copied verbatim (toolbar labels, sliders, actions, panel/search/empty-state strings, cart confirmation).

## Concerns (non-blocking, for final review — matching the project's established "Minor" logging pattern from Tasks 1–10)

1. **`ClearMaskAsync` doesn't reset `_corners`** (brief's own given code, copied verbatim). Scenario: user segments a mask, toggles «Перспектива» (which lazily populates `_corners` from `nsiVisualizer.defaultCornersFromMask` the first time, since it only fetches when `_corners[2] == 0 && _corners[5] == 0`), drags handles, then clicks «Изчисти» and re-segments a new mask. `_corners` is never zeroed by `ClearMaskAsync`, so the next «Перспектива» toggle sees non-zero values and skips re-fetching defaults for the *new* mask — the handle overlay would show stale corner positions from the previous mask/segmentation. Did not fix, since it's the brief's own specified code verbatim and not something Task 11 asked to redesign; flagging for whoever reviews Task 11 or plans Task 14's E2E pass.
2. **Perspective-handle SVG overlay may intercept pointer events over the paved area.** The brief's CSS gives `pointer-events: all` only to `.viz-handle` (the draggable circles); `.viz-grid-outline`/`.viz-grid-line` (and the `<svg>` container) get no explicit `pointer-events` rule, so they inherit the SVG default (`auto`/`visiblePainted`). Since `.viz-grid-outline` has a non-transparent fill, the browser will likely hit-test pointer events against the filled quad interior before they reach `editCanvas` underneath — meaning while «Перспектива» is toggled on, tap/brush edits inside the quad region may not reach the mask-editing canvas. This looks like probably-intentional UX (handles mode temporarily takes over from mask editing) rather than an oversight, and is exactly the brief's given CSS/markup, so left as specified. Noting for Task 14's manual E2E pass to confirm the intended interaction model.
3. **`OnHandleUp`/`_dragIndex` reset is scoped to the wrapping `<div>`'s `pointerup`.** If the user releases the pointer outside that div's bounds mid-drag (e.g., drags past the viewport edge), `_dragIndex` could stay set until the next pointerup fires anywhere inside the div, causing an unexpected jump on the next pointermove. Matches the brief's given code verbatim; flagging as a minor UX edge case, not fixed.

None of the above are build/test-blocking; all three are exactly the brief's specified code, preserved verbatim per the task's "complete code to add, verbatim where possible" instruction.

## Fix Report

Review follow-up: four Important defects fixed (all in the brief's verbatim code; fixes authorized by the coordinator), commit `8054aab`. Files: `src/NaturalStoneImpex.Client/Pages/Public/Visualizer.razor`, `src/NaturalStoneImpex.Client/wwwroot/css/app.css`.

### 1. Culture-corrupted SVG/slider markup

Blazor WASM adopts the browser locale; under bg-BG an interpolated `double` renders as `"843,27"` — SVG treats the comma as a coordinate separator (corrupt polygon/line/circle geometry) and range-input `value` attributes require dot decimals. Fixed locally (global culture unchanged — changing it would alter existing price formatting site-wide) with a private helper at `Visualizer.razor:352`:

```csharp
private static string Inv(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
```

Every interpolation routed through `Inv()` (all in `src/NaturalStoneImpex.Client/Pages/Public/Visualizer.razor`):

| Location | Interpolation(s) |
|---|---|
| line 86 | `<line x1="@Inv(_corners[0])" y1="@Inv(_corners[1])" x2="@Inv(_corners[6])" y2="@Inv(_corners[7])"` |
| line 87 | `<line x1="@Inv(_corners[2])" y1="@Inv(_corners[3])" x2="@Inv(_corners[4])" y2="@Inv(_corners[5])"` |
| line 91 | `<circle cx="@Inv(_corners[index * 2])" cy="@Inv(_corners[index * 2 + 1])" r="@Inv(_photoW * 0.02)"` |
| line 114 | scale slider `value="@Inv(_scale)"` |
| line 119 | rotation slider `value="@Inv(_rotation)"` |
| line 124 | compare slider `value="@Inv(_compare)"` |
| line 355 | `HandlePolygon` property — all 8 `_corners` values wrapped in `Inv()` inside the interpolated string (feeds `points="@HandlePolygon"` at line 85) |

Full-file audit: **no other fractional interpolation remains in the page markup** — the only remaining numeric interpolations are integers, which have no decimal separator: `value="@_brushSize"` (int, line 74), `viewBox="0 0 @_photoW @_photoH"` (ints, line 84), and `href="/products/@_selected.Id"` (int, line 133).

### 2. Stale corners after «Изчисти»

- (a) `ClearMaskAsync` now also does `_corners = new double[8];` so `ToggleHandlesAsync`'s "keep user-adjusted values" heuristic (`_corners[2] == 0 && _corners[5] == 0`) correctly re-fetches defaults for a freshly-segmented mask.
- (b) `OnCanvasTapAsync`'s first-success block now assigns the fetched defaults to C# state (`_corners = corners;`) before sending them via `setCorners`, so the page mirrors JS geometry immediately; the Toggle heuristic remains as a fallback (e.g. mask created purely via brush without a segment call).

This resolves the report's original Concern 1.

### 3. Handle overlay swallowed stage input

`app.css`: added `pointer-events: none;` to `.viz-handles` (the SVG container). `.viz-handle { pointer-events: all; }` is unchanged, so the corner circles stay draggable, but taps/brush strokes under the filled quad now pass through to the edit canvas while «Перспектива» is visible — consistent with the toolbar staying enabled in that state. Resolves the report's original Concern 2.

### 4. Stale drag state on release outside the wrapper

Added `@onpointerleave="OnHandleUp"` to the wrapper div (alongside the existing `@onpointermove`/`@onpointerup`), so leaving the area mid-drag ends the drag instead of leaving `_dragIndex` set. Resolves the report's original Concern 3.

### Verification

- `dotnet build` (whole solution): **Build succeeded. 0 Warning(s), 0 Error(s)**.
- `dotnet test`: **Passed! Failed: 0, Passed: 19, Skipped: 0, Total: 19**.
- Only `Visualizer.razor` and `app.css` changed; Task 10's three approved fixes (manual query parser, `OnCanvasTapAsync` JSException catch, `OnPhotoSelectedAsync` error scopes) remain untouched.
