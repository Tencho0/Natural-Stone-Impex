# Task 10 Report — Visualizer page: upload, tap-to-segment, render (happy path)

## What was implemented

- Created `src/NaturalStoneImpex.Client/Pages/Public/Visualizer.razor` per the brief's Step 1, with one deviation documented below (QueryHelpers fallback).
- Appended the `.viz-overlay` rule to `src/NaturalStoneImpex.Client/wwwroot/css/app.css` (Step 2), verbatim from the brief. Confirmed `wwwroot/index.html` references `css/app.css` (and `css/site.css`, `NaturalStoneImpex.Client.styles.css`) — `app.css` was the correct target, matching the brief's assumption.
- Route `/visualizer`, page class `Visualizer`, `[JSInvokable]` methods `OnCanvasTapAsync(double x, double y, int label)` and `OnMaskEditedAsync()` exposed exactly as required.

## Deviation from the brief: QueryHelpers fallback

The brief's Step 1 code uses `Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(...)`. Building the brief's code verbatim failed:

```
error CS0234: The type or namespace name 'WebUtilities' does not exist in the namespace 'Microsoft.AspNetCore'
```

Investigation: `Microsoft.AspNetCore.WebUtilities.dll` exists in the resolved asset graph (`obj/project.assets.json`) only under a `tools/` path — it's an internal runtime asset bundled inside the `Microsoft.AspNetCore.Components.WebAssembly.DevServer` package (used to run the dev Kestrel server via `dotnet run`), not a `compile`/`lib` asset exposed to application code. No package reference resolves it as a referenceable assembly for the Client project, and no such DLL exists anywhere else on disk. So `QueryHelpers` is genuinely unavailable to this Blazor WASM client at compile time.

Fix applied (page side only, per instructions — JS untouched): removed the `@using Microsoft.AspNetCore.WebUtilities` line and replaced the `QueryHelpers.ParseQuery(...)` call with a small private static helper, `ParseProductIdFromQuery(string uri)`, that manually splits the query string on `?`, `&`, `=` and does `int.TryParse` on the `productId` value (with `Uri.UnescapeDataString` for safety). Behavior is identical for the `?productId=N` contract Task 12 depends on. This is documented in an inline code comment at the helper's definition.

Everything else in the page (fields, methods, markup, Bulgarian strings, JS interop call sequence) is copied verbatim from the brief.

## Verification evidence

**Build** (`dotnet build`, whole solution, from repo root):
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:10.35
```
All four projects built: `NaturalStoneImpex.Api`, `NaturalStoneImpex.Api.Tests`, `NaturalStoneImpex.Client` (+ Blazor output). No warnings from the new/modified files (or anywhere else).

**Tests** (`dotnet test`):
```
Passed!  - Failed: 0, Passed: 19, Skipped: 0, Total: 19, Duration: 2 s
```
Matches the expected 19 green tests. (No new tests were added/expected for this task — it's a Razor page with no dedicated unit test target per the task brief.)

**Live manual verification (Step 3 of the brief) was NOT run** — no reachable SQL Server for the API in this environment. Deferred to the plan's Task 14 E2E checklist, as instructed.

## Interop cross-check against `wwwroot/js/visualizer.js`

Verified every call site in the page against the actual JS implementation (read in full):

| Page call | JS signature | Match |
|---|---|---|
| `JS.InvokeAsync<object>("nsiVisualizer.init", "viz-stage", _selfRef, (object?)null)` | `init: function (stageId, ref, options)` → `{webgl}` | OK, 3 args |
| `JS.InvokeAsync<object>("nsiVisualizer.loadPhotoFromDataUrl", dataUrl)` | `loadPhotoFromDataUrl: function (dataUrl)` → Promise `{width,height}` | OK, return value unused here (fine — Task 11 territory) |
| `JS.InvokeVoidAsync("nsiVisualizer.setMode", "tap-add")` | `api.setMode = function (m)` | OK |
| `JS.InvokeVoidAsync("nsiVisualizer.setMaskVisible", true)` | `setMaskVisible: function (visible)` | OK |
| `JS.InvokeAsync<object>("nsiVisualizer.setMaskPng", result.MaskPng)` | `setMaskPng: function (base64)` → Promise | OK |
| `JS.InvokeAsync<double[]>("nsiVisualizer.defaultCornersFromMask")` | `defaultCornersFromMask: function ()` → plain array (not a Promise) | OK — JS interop resolves non-Promise return values as immediately-completed tasks, marshals fine |
| `JS.InvokeVoidAsync("nsiVisualizer.setCorners", (object)corners)` | `setCorners: function (c)` | OK |
| `JS.InvokeAsync<object>("nsiVisualizer.setProductTexture", url, widthMeters)` | `setProductTexture: function (url, widthMeters)` → Promise | OK, 2 args |
| `JS.InvokeVoidAsync("nsiVisualizer.render")` | `render: function ()` | OK |
| `JS.InvokeVoidAsync("nsiVisualizer.dispose")` | `dispose: function ()` | OK |

JS-to-.NET callbacks (in `onPointerUp`/`finalizeStroke` in visualizer.js):
- `dotNetRef.invokeMethodAsync('OnCanvasTapAsync', p.x, p.y, mode === 'tap-add' ? 1 : 0)` → matches page's `[JSInvokable] public async Task OnCanvasTapAsync(double x, double y, int label)` exactly (name, arg count, arg order/types: x, y, int label).
- `dotNetRef.invokeMethodAsync('OnMaskEditedAsync')` → matches page's `[JSInvokable] public Task OnMaskEditedAsync()` exactly (no args).

No mismatches found; nothing needed to change on the JS side.

## Self-review checklist

- **Interop names/arity**: all confirmed above — no mismatches.
- **DotNetObjectReference disposed?** Yes — `_selfRef?.Dispose()` in `DisposeAsync`.
- **JSDisconnectedException handled on dispose?** Yes, in `DisposeAsync`: `try { await JS.InvokeVoidAsync("nsiVisualizer.dispose"); } catch (JSDisconnectedException) { }`. (Note: `ResetAsync`'s own call to `nsiVisualizer.dispose` is NOT wrapped in try/catch — this matches the brief's given code verbatim; `ResetAsync` only runs from a live button click on a mounted component, so a disconnected JS runtime there is not an expected/reachable path the way it is during page teardown.)
- **Bulgarian strings byte-exact?** Yes — every UI string (consent text, guidance, buttons, errors, alerts, `PageTitle`) copied verbatim from the brief. Verified UTF-8 encoding intact (`file` reports "Unicode text, UTF-8 text"; spot-checked "Съгласен" and "Визуализатор" substrings present).
- **Consent gates the InputFile?** Yes — `disabled="@(!_consent || _busy)"` on `<InputFile>`.
- **Errors shown via alert?** Yes — `alert-danger` for errors, `alert-info`/`alert-primary` for guidance, matching Bootstrap 5 conventions used elsewhere (e.g. `ProductDetail.razor`).
- **Tap flow**: first tap (`_sessionToken is null`) → `SegmentAsync`; subsequent taps → `RefineAsync`; on `expired` → transparent re-upload via `SegmentAsync` with the kept `_photoBytes` and accumulated `_points`; on any null result → the just-added point is removed from `_points` (`_points.RemoveAt(_points.Count - 1)`) and a Bulgarian error is shown. Confirmed this matches the brief exactly.

## Files changed

- `src/NaturalStoneImpex.Client/Pages/Public/Visualizer.razor` (new)
- `src/NaturalStoneImpex.Client/wwwroot/css/app.css` (appended `.viz-overlay`)

## Concerns

- QueryHelpers unavailability is a real environment constraint (not just a "couldn't verify" situation) — documented above and fixed with an equivalent manual parser. If a later task (e.g. Task 12, which also touches `?productId=N`) assumes `QueryHelpers` is importable elsewhere in the client, it will hit the same compile error and should reuse `ParseProductIdFromQuery` or an equivalent instead.
- Live browser verification (Step 3) is deferred to Task 14's E2E checklist per the environment constraints — no SQL Server reachable here to run the API.

## Fix Report

Review follow-up: three Important issues fixed in `src/NaturalStoneImpex.Client/Pages/Public/Visualizer.razor`, commit `73c672b`.

### 1. Fragment bug in query parser

`ParseProductIdFromQuery` now parses from `new Uri(uri).Query` instead of manually indexing `'?'` on the raw URI. `System.Uri` separates the fragment into `.Fragment` per RFC 3986, so `.Query` never contains `#...`. The rest of the helper (split on `'&'`, `Split('=', 2)`, `Uri.UnescapeDataString` + `int.TryParse`, null on absent/non-numeric) is unchanged. `Navigation.Uri` is always absolute in Blazor, so the `Uri` constructor is safe.

Case walkthrough (mental verification):

| Input (after `/visualizer`) | `.Query` | Result |
|---|---|---|
| *(no query)* | `""` → TrimStart → `""` → 0 pairs | `null` → fallback to first product |
| `?productId=5` | `"?productId=5"` → `["productId","5"]` | `5` |
| `?a=1&productId=7` | pairs `a=1`, `productId=7`; first key skipped | `7` |
| `?productId=abc` | `TryParse("abc")` fails → loop exhausts | `null` → fallback |
| `?productId=5#frag` | `.Query` = `"?productId=5"` (fragment excluded by `System.Uri`) | `5` — previously `TryParse("5#frag")` failed and wrongly fell back |

### 2. JS interop catch in OnCanvasTapAsync

Added `catch (JSException)` between the try block and the existing finally: sets `_error = "Възникна грешка при показването на визуализацията. Моля, опитайте отново.";`. A rejected JS promise (e.g. `setMaskPng` on a corrupt PNG) now surfaces as the page's Bulgarian danger alert instead of crashing to Blazor's global error banner. The finally (`_busy = false; StateHasChanged();`) is unchanged, so the busy overlay always clears.

### 3. InitStageAsync failures no longer misreported as bad-file

`OnPhotoSelectedAsync` restructured: the file-read pipeline (RequestImageFileAsync → stream copy → `_photoBytes`) keeps its own `catch (Exception)` with the bad-file message and now `return`s early on failure; `InitStageAsync()` is wrapped in a separate `try/catch (JSException)` that sets `_error = "Възникна грешка при зареждането на визуализатора. Моля, опитайте отново.";` and `_photoLoaded = false;` (returns the user to the upload card with the error visible). Both blocks are nested inside an outer `try { ... } finally { _busy = false; }`, so the busy flag is cleared on every path — file-read success, file-read failure (early return), init success, init failure. `JSDisconnectedException` derives from `JSException`, so it is covered too.

### Verification

- `dotnet build` (whole solution): **Build succeeded. 0 Warning(s), 0 Error(s)**.
- `dotnet test`: **Passed! Failed: 0, Passed: 19, Skipped: 0, Total: 19**.
- Only `Visualizer.razor` changed (32 insertions, 16 deletions); working tree clean after commit.
