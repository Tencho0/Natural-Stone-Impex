# Task 7 Report: visualizer.js — homography + WebGL rendering core

## Status: DONE

## What was implemented

1. **`tests/manual/visualizer-harness.html`** — the brief's harness verbatim, plus a pixel-level
   rendering-quality extension (see below) so headless verification doesn't have to rely on
   eyeballing the canvas.
2. **`src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js`** — implemented byte-for-byte identical
   to the brief's code block (verified with `diff` against the extracted brief snippet — no
   corrections were necessary; see "Brief-code fixes" below).
3. **`src/NaturalStoneImpex.Client/wwwroot/index.html`** — added `<script src="js/visualizer.js"></script>`
   immediately before the `blazor.webassembly.js` script tag.

Public API surface exposed on `window.nsiVisualizer` matches the brief exactly: `init`,
`loadPhotoFromDataUrl`, `setMaskPng`, `clearMask`, `hasMask`, `setMaskVisible`,
`defaultCornersFromMask`, `setCorners`, `setProductTexture`, `setScale`, `setRotation`, `render`,
`setCompareRatio`, `exportResultDataUrl`, `downloadResult`, `dispose`, `_test.computeHomography`,
`_test.applyH`, `_internal()`. The `api._wireEvents` hook (Task 8) and `_internal()` accessor are
present and untouched. `getStageRect` was intentionally NOT added (Task 11). `GROUND_W = 10`,
`GROUND_H = 15`, corner order TL/TR/BR/BL, and the 45%-of-bbox-width default top edge all match
the spec.

## Harness extension (pixel-level assertions)

Since headless Chromium can't be visually judged, I added a block (guarded by `if (mode.webgl)`,
run only on the default/non-`?fallback` path) right after `viz.render()`:

- Uses `viz._internal().glCanvas.getContext("webgl")` to grab the *existing* WebGL context (canvas
  contexts are cached per canvas+type by the HTML spec, so this returns the same context the
  module created, no re-init).
- `readBlock(cx, cy, half)` reads a `(2*half+1)²` block via `gl.readPixels` and averages RGBA,
  converting from top-left "photo pixel" coordinates (matching the module's own convention for
  corners/masks) to WebGL's bottom-left-origin `readPixels` coordinates via `H - 1 - cy`. Averaging
  over a large block (51×51 for the two masked samples) makes the assertions robust against
  landing on a grid line vs. a fill pixel of the procedural checkerboard stone texture.
- Three checks, all added to the `failures` array so "ALL PASS" covers them:
  - (a) `(600, 800)` — deep inside the masked trapezoid, in the lit (non-shadow) region: asserts
    `alpha > 0` (texture actually drawn) and that the average color is *not* the flat driveway gray
    `#8a8a86` (138,138,134) — i.e., the stone texture, not the untouched photo, is what's visible.
  - (b) `(50, 50)` — sky region, well outside the mask: asserts `alpha === 0`.
  - (c) `(600, 660)` — inside the mask *and* inside the dark shadow band rect (`y: 600–720`):
    asserts `alpha > 0` and that its luminance is strictly less than the lit pixel's luminance
    (confirms the luminance-transfer shading term is doing real work, not just passing through a
    flat multiplier).
- Also added `assert(mode.webgl, "WebGL context initialized (headless)")` right after `viz.init(...)`
  (only when not `?fallback`) so a headless environment that silently falls back to no-WebGL would
  surface as a `FAIL` instead of masking the pixel checks (which are skipped in that case).

All original brief assertions were kept completely intact; only additions were made.

## Harness verification evidence

### RED (Step 2) — before `visualizer.js` existed

Command:
```
& "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --headless=new --disable-gpu-sandbox `
  --virtual-time-budget=5000 --dump-dom `
  "file:///C:/Users/TenchoBostandzhiev/source/GitHub%20-Tencho%20Bostandzhiev/Natural-Stone-Impex/tests/manual/visualizer-harness.html"
```
Relevant DOM output:
```html
<p id="status" class="fail">FAIL: module loaded; exception: Cannot read properties of undefined (reading '_test')</p>
```
Confirms the harness correctly fails when the module is missing.

### GREEN (Step 4) — after implementing `visualizer.js`

Same command (no extra WebGL flags needed — headless Edge's default new headless mode already
provides software WebGL):
```
& "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --headless=new --disable-gpu-sandbox `
  --virtual-time-budget=5000 --dump-dom `
  "file:///C:/Users/TenchoBostandzhiev/source/GitHub%20-Tencho%20Bostandzhiev/Natural-Stone-Impex/tests/manual/visualizer-harness.html"
```
Relevant DOM output:
```html
<title>visualizer.js harness (webgl)</title>
...
<p id="status" class="pass">ALL PASS — now verify visually: stones must recede with perspective, shadow band must remain visible on the stones.</p>
```
The `(webgl)` title suffix confirms `mode.webgl === true`, i.e., the pixel-level assertions block
did run (it's gated on `mode.webgl`) and passed — not skipped.

I additionally re-ran with `--enable-unsafe-swiftshader --use-gl=angle` added (the environment
note's suggested fallback flags) and got the identical `ALL PASS` result, confirming those flags
aren't strictly required in this environment but are harmless if needed elsewhere.

### Informational: `?fallback` path (out of scope for Task 7)

Ran the harness with `?fallback` appended (forces `forceFallback: true`, so `gl` stays `null` and
`render()` calls `api._renderFallback()`):
```html
<p id="status" class="fail">FAIL: exception: api._renderFallback is not a function</p>
```
Expected and correct — the brief explicitly defers canvas-2D fallback rendering to Task 8
("`_renderFallback`" is referenced but not implemented in this task's code). Not a bug; not fixed;
noted here only for completeness. The default (non-fallback) path — the one that matters for this
task — is fully green.

## Brief-code fixes

None. `visualizer.js` was implemented byte-identical to the brief's code block (verified via
`diff` against the block extracted from `task-7-brief.md`). No shader compile errors, no
matrix-majority mix-ups, no other bugs surfaced during harness verification — including under the
new pixel-level assertions that specifically probe the homography-inverse + mask-clip +
luminance-transfer pipeline. The geometry contract holds:
- `corners` (photo px, top-left origin, TL/TR/BR/BL) → `groundToPx` homography (ground meters →
  photo px) → `pxToGround = invert3(groundToPx)` (photo px → ground meters), uploaded to the
  shader as `u_invH` (converted row-major → column-major for `uniformMatrix3fv`).
- The shader's `uv = vec2(v_uv.x, 1.0 - v_uv.y)` flip correctly reconciles WebGL's bottom-left
  NDC/window convention with the top-left convention used for photo/mask canvas pixels and for
  the `corners` array, so `px = uv * u_size` in the shader lands in the same top-left pixel space
  as `defaultCornersFromMask()`/`setCorners()`. Verified empirically by the pixel assertions
  above (sampling at photo-pixel coordinates I computed the "lit"/"shadow"/"outside" regions to
  land in, and getting exactly the expected alpha/luminance relationships).

## Files changed

- `src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js` (new)
- `tests/manual/visualizer-harness.html` (new)
- `src/NaturalStoneImpex.Client/wwwroot/index.html` (modified — added script tag before
  `blazor.webassembly.js`)

## Self-review findings

- Confirmed `dotnet build` succeeds with 0 warnings / 0 errors.
- Confirmed `git status`/`git diff --stat` before commit showed only the 3 intended files (no
  stray scratch files staged).
- Confirmed the committed `visualizer.js` is byte-identical to the brief's code block (see above).
- Confirmed all brief-mandated public API names are present with no typos, `getStageRect` was
  correctly NOT added, and `GROUND_W`/`GROUND_H`/corner order/45% default all match spec.
- Confirmed the `?fallback` failure is an expected Task-8 gap, not a Task-7 defect, and left it
  untouched per the instruction not to restructure anything.
- Line endings: git warned about LF→CRLF normalization on the two new text files (pre-existing
  repo `.gitattributes`/core.autocrlf behavior) — cosmetic only, no action needed.

## Concerns

None blocking. Two small forward-looking notes for whoever picks up Task 8:
- `api._renderFallback` doesn't exist yet — `render()` will throw if `forceFallback: true` or if
  WebGL init fails, until Task 8 adds it.
- `api._wireEvents` is called (if present) at the end of `init()`, exactly as the brief specifies,
  ready for Task 8's pointer-interaction layer to install itself before `init()` returns.

## Commit

`0ff8205` — `feat(visualizer): WebGL rendering engine with homography and luminance transfer`
(branch `feature/visualizer`)

## Fix Report

Reviewer found two Important lifecycle defects (both inherited from the brief's code). Fixed in
commit `daf95fc` — `fix(visualizer): reset engine state on dispose and release WebGL context on
re-init`. Public API surface unchanged (same names, same signatures, same return shapes).

### Defect 1: dispose() left stale non-GL state

After `dispose()` → `init()`, `maskPresent`, `corners`, `groundToPx`, `pxToGround`, `tileSource`,
`blurredMask`, `maskCanvas`, `photoW/photoH`, `lumMean` retained old values, so `render()`'s guard
(`!maskPresent || !tileSource || !pxToGround`) could pass on stale data and render with nulled
textures / a previous photo's homography.

Fix: added a private `resetState()` in a new "lifecycle" section of
`src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js` that resets all of the above
(`maskPresent=false`; `corners`/`groundToPx`/`pxToGround`/`tileSource`/`blurredMask`/`maskCanvas`
= null; `photoW=photoH=0`; `lumMean=0.5`; `scaleFactor=1`; `rotationRad=0`; also `tileMeters=1.0`,
same class of stale state). Called from `dispose()` (in addition to the existing DOM cleanup) and
from the top of `init()` (covers Blazor re-mounts that never called `dispose()`).

### Defect 2: init() never tore down a prior WebGL context

Repeated `init()` calls leaked WebGL contexts up to the browser cap (~16), after which
`getContext('webgl')` returns null and `render()` would call the not-yet-existing
`_renderFallback` — an uncaught TypeError.

Fix: added a private `releaseGl()` that, if a `gl` exists, calls
`gl.getExtension('WEBGL_lose_context').loseContext()` (when the extension is available), then
nulls `gl`, `program`, `photoTexture`, `maskTexture`, `tileTexture`. Called at the top of `init()`
and from `dispose()` (before the state reset), exactly as prescribed.

### Harness regression check

Added to `tests/manual/visualizer-harness.html`, at the end of the assertion block (after the
export + pixel assertions, before the slider wiring), keeping all existing assertions intact:
`viz.dispose()`, then `viz.init("stage", null, { forceFallback: false })`, then
`assert(viz.hasMask() === false, ...)` and a try/catch around `viz.render()` that pushes a failure
on throw. It runs last, so the stage being left re-initialized without a photo does not affect any
earlier visual/pixel assertion.

### Verification evidence

GREEN — headless run with the fix in place:
```
& "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --headless=new --disable-gpu-sandbox `
  --virtual-time-budget=5000 --dump-dom `
  "file:///C:/Users/TenchoBostandzhiev/source/GitHub%20-Tencho%20Bostandzhiev/Natural-Stone-Impex/tests/manual/visualizer-harness.html"
```
```html
<title>visualizer.js harness (webgl)</title>
<p id="status" class="pass">ALL PASS — now verify visually: stones must recede with perspective, shadow band must remain visible on the stones.</p>
```

RED cross-check — to prove the new regression assertion actually covers defect 1, the fixed
harness was run against the PRE-fix `visualizer.js` (via `git stash push -- ...js`, run, `git
stash pop`):
```html
<p id="status" class="fail">FAIL: hasMask() is false after dispose + re-init</p>
```
So the new assertion fails on the old code and passes on the fixed code.

`dotnet build src/NaturalStoneImpex.Client`: Build succeeded, 0 Warning(s), 0 Error(s).

### Files changed in the fix

- `src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js` (+`resetState()`/`releaseGl()`, wired
  into `init()` and `dispose()`)
- `tests/manual/visualizer-harness.html` (dispose/re-init regression assertions)

### Notes

- `resetState()` intentionally does NOT touch `stage`/`photoImg`/`glCanvas`/`editCanvas` — `init()`
  recreates them, and `dispose()` already empties the stage; nulling them was not requested and
  Task 8's `_wireEvents` hook contract is untouched.
- Defect 2's context-release path is exercised implicitly by the regression check (dispose →
  loseContext → re-init acquires a fresh context and `mode.webgl` handling still works); the
  ~16-context browser cap itself is impractical to hit deterministically in the harness.
