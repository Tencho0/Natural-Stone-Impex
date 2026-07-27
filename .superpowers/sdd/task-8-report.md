# Task 8 Report: visualizer.js — taps, brush editing, canvas-2D fallback

## Summary

Implemented the interaction layer (`setMode`, `setBrushSize`, tap callbacks,
brush/erase mask painting) and the canvas-2D fallback renderer (`api._renderFallback`)
in `src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js`, exactly as specified in
`.superpowers/sdd/task-8-brief.md`, integrated with the current file's
`resetState()`/`releaseGl()` lifecycle helpers added by the prior fix commit.
Extended `tests/manual/visualizer-harness.html` per the brief, plus an additional
(successful) extension of the pixel-level render assertions to also run on the
fallback path. Found and fixed a genuine rendering bug in the fallback renderer
during verification (see "Bug found and fixed" below). Both render paths verified
headlessly: **ALL PASS**.

## Files changed

- `src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js`
  - `resetState()`: added reset of the new interaction state (`mode = null; stroking
    = false; strokeMoved = false;`) so a fresh `init()` doesn't inherit an active
    drawing mode or a mid-stroke flag from a prior session (per task instructions —
    `resetState` is the right place for this since these are mask-editing-adjacent
    like the other mask fields it already resets).
  - New "interaction layer" section (before `return api;`): `eventToPhotoPx`,
    `paintAt`, `onPointerDown/Move/Up`, `api.setMode`, `api.setBrushSize`,
    `api._wireEvents`, `api._test.strokeForTest` — copied from the brief verbatim.
  - New "canvas-2D fallback renderer" section: `drawTriangle` (affine warp helper)
    and `api._renderFallback` — copied from the brief, with one fix applied inside
    (see below).
- `tests/manual/visualizer-harness.html`
  - Added the brief's Step 1 interaction-layer assertions (setMode/setBrushSize
    existence, brush-paints-the-mask regression check) after the
    `exportResultDataUrl` assertion.
  - Extended the existing pixel-level render assertions (previously gated on
    `mode.webgl` only) to also run in fallback mode via `getImageData` on the same
    `glCanvas` (reused as a 2D canvas by `_renderFallback`). Kept the WebGL
    `readPixels` path untouched; added a parallel `readBlock` implementation for
    the 2D path (top-left origin, no Y-flip needed, unlike `readPixels`). Slightly
    loosened the "outside mask alpha" assertion for the fallback path only
    (`< 2` instead of `=== 0`) to tolerate the mask's 3px feather blur; kept the
    WebGL path's exact `=== 0`.

## Coordinate/integration check (brush regression point)

The brief specifies `viz._test.strokeForTest(60, 420)` and reading the mask pixel
at `(60, 420)` before/after. Checked this against the harness's procedural mask
trapezoid (`moveTo(430,400) → (760,400) → (1050,880) → (180,880)`): at `y = 420`
the trapezoid's left edge is at `x ≈ 419.6` (parametrized along the
`(430,400)→(180,880)` edge), so `x = 60` is well outside the trapezoid (mask = 0
there) before the stroke, and directly under the 60px-diameter brush circle after
it. **No coordinate adjustment was needed** — `(60, 420)` is genuinely unselected
before and inside the canvas (1200×900 procedural photo), exactly as the brief
assumed.

## Bug found and fixed (fallback renderer)

While verifying the `?fallback=1` path, the extended "outside mask" assertion
failed: pixel `(50,50)` (sky, far from the paved trapezoid) read back as fully
opaque white (`rgba 255,255,255,255`) instead of transparent. Root cause, found
via targeted debug output before finalizing:

1. The luminance-transfer step (`pctx.globalCompositeOperation = 'multiply';
   pctx.drawImage(photoImg, ...)`) draws the **fully opaque** photo over the whole
   `pavedLayer` canvas. Per Porter-Duff compositing, `alpha_out = alpha_src +
   alpha_dst * (1 - alpha_src)`; with `alpha_src = 1` (opaque photo), `alpha_out =
   1` **everywhere**, regardless of blend mode or the pre-existing per-pixel alpha
   from the triangle warp. So this step alone makes the entire canvas opaque, not
   just the paved region.
2. The subsequent `pctx.globalCompositeOperation = 'destination-in';
   pctx.drawImage(blurredMask, 0, 0);` was meant to clip that back down to the
   mask shape — but `blurredMask`/`maskCanvas` are opaque black/white rasters
   (alpha = 255 everywhere; the *red channel* is the actual selection signal, the
   same convention the WebGL shader uses via `texture2D(u_mask, uv).r`). Since
   `destination-in` composites against the **source's alpha**, and that alpha is
   uniformly 255, the clip was a no-op — it never actually masked anything.

Net effect: the fallback renderer's output was opaque across the entire canvas,
not just the paved trapezoid — a real, visually-broken bug (would have painted
stone-tinted sky/grass in production), not just a test artifact.

**Fix applied** (minimal, contained to `_renderFallback`): replaced the
`destination-in` + `drawImage(blurredMask)` composite trick with an explicit
pixel-level alpha multiply using the mask's red channel:

```javascript
var maskPixels = blurredMask.getContext('2d').getImageData(0, 0, photoW, photoH);
var pavedPixels = pctx.getImageData(0, 0, photoW, photoH);
var md = maskPixels.data, pd = pavedPixels.data;
for (var i = 0; i < pd.length; i += 4) pd[i + 3] = pd[i + 3] * md[i] / 255;
pctx.putImageData(pavedPixels, 0, 0);
```

This does not touch `rebuildMaskDerived`/`blurredMask`'s construction (shared with
the already-verified WebGL path from Task 7 — its shader reads `.r` the same way),
so the WebGL path is unaffected. Verified via a standalone visual check (see
below) that the fallback render now correctly shows the paved trapezoid only
within the mask, with feathered edges, and sky/grass elsewhere untouched.

## RED evidence (before implementing Step 2)

Command:
```
"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --headless=new --disable-gpu-sandbox --virtual-time-budget=5000 --dump-dom "<file URL>/tests/manual/visualizer-harness.html"
```
Result:
```
<p id="status" class="fail">FAIL: setMode exists; setBrushSize exists; exception: viz.setMode is not a function</p>
```

## GREEN evidence (both paths, after implementation + bug fix)

WebGL path (`visualizer-harness.html`, `--virtual-time-budget=5000`):
```
<title>visualizer.js harness (webgl)</title>
<p id="status" class="pass">ALL PASS — now verify visually: stones must recede with perspective, shadow band must remain visible on the stones.</p>
```

Fallback path (`visualizer-harness.html?fallback=1`, `--virtual-time-budget=8000`
— raised from 5000 because the fallback path does more CPU work: pattern tiling,
144 clipped/transformed triangle draws, and two full-canvas `getImageData`/
`putImageData` passes):
```
<title>visualizer.js harness (canvas-2d)</title>
<p id="status" class="pass">ALL PASS — now verify visually: stones must recede with perspective, shadow band must remain visible on the stones.</p>
```

## Visual sanity check

Built a standalone scratch page (not part of the repo) that runs the same
photo/mask/texture/corners setup as the harness, calls `viz.setMaskVisible(false)`
to hide the green edit-tint overlay, and renders both paths side by side. Both
produced the expected trapezoidal stone-tile pavement receding with perspective,
grid lines converging correctly, and the dark shadow band still visible on the
stones (matching the harness's "verify visually" hint). The canvas-2D fallback
output is visually very close to the WebGL output — marginally softer at the
mask edges (expected, due to the 3px feather blur and 12×12 cell triangulation
vs. true per-pixel projective mapping) but not visibly broken or discolored.

## dotnet build

```
dotnet build src/NaturalStoneImpex.Client
```
Result: `Build succeeded. 0 Warning(s). 0 Error(s).`

## Self-review

- Public API additions match the global constraint exactly: `setMode`,
  `setBrushSize`, `api._wireEvents`, `api._renderFallback`,
  `api._test.strokeForTest` — no other public surface added.
- `mode`/`stroking`/`strokeMoved` reset added to `resetState()`; `brushSize` is
  left untouched across `init()` calls (a user brush-size preference, not
  mask-related state) — this matches the task's framing ("mask-related state").
  `api._wireEvents` re-binds `pointerdown/move/up` on the freshly-created
  `editCanvas` element inside each `init()`, so there's no listener leak/duplication
  across dispose/re-init cycles (the old canvas element, and its listeners, are
  discarded along with the old DOM subtree).
- Tap handlers guard on `dotNetRef` being present (it's nulled in `dispose()`), so
  no crash if a stray pointer event fires during teardown.
- Comments are in English; no new external dependencies.
- The one deviation from the brief's literal code is the bug fix inside
  `_renderFallback`'s mask-clipping step, documented above with rationale; the
  rest of the fallback renderer (ground-texture tiling, homography-warped
  triangle grid, luminance transfer approach) is unchanged from the brief.
- The harness's fallback-path pixel-assertion extension (not explicitly required,
  but suggested as "if straightforward") worked out and is included; the only
  relaxation is a `< 2` alpha tolerance (vs. exact `=== 0`) outside the mask on
  the fallback path only, to account for the 3px mask feather — the WebGL path
  keeps the original exact assertion.

## Concerns

None blocking. Minor, non-blocking notes for whoever picks up Tasks 10–11:
- The fallback renderer's per-frame cost (144 clipped `drawTriangle` calls +
  two full-resolution `getImageData`/`putImageData` passes) is noticeably heavier
  than the WebGL path. It only runs when WebGL is genuinely unavailable, so this
  is likely acceptable, but if `render()` were ever called at high frequency in
  fallback mode (e.g. from a live slider drag), it may be worth throttling.
- `defaultCornersFromMask()`'s inscribed trapezoid (45% top width) can extend
  slightly beyond the true segmentation polygon at the top corners; this is
  pre-existing Task 7 behavior (not part of this task) and is masked out
  correctly by the mask-clip step in both render paths regardless.

## Fix Report

**Defect (reviewer, Important):** in the interaction layer, `setMode(null)` (or any
tool switch) while a pointer was down mid-brush-stroke left `stroking = true` with
stale semantics: `onPointerMove` kept painting (always with 'add' semantics once
the mode was no longer 'erase'), and `onPointerUp` bailed on its leading
`if (!mode || !photoW) return;` guard, so `rebuildMaskDerived()` / `render()` /
`OnMaskEditedAsync` never fired for that stroke — the edit was painted into
`maskCanvas` but never committed or reported to .NET.

**Fix (commit `755643e`, public API unchanged):**
- Extracted private `finalizeStroke()` — `if (!stroking) return; stroking = false;
  rebuildMaskDerived(); api.render(); if (dotNetRef)
  dotNetRef.invokeMethodAsync('OnMaskEditedAsync');`.
- `api.setMode(m)` now calls `finalizeStroke()` before assigning the new mode:
  a live stroke is committed the moment the tool changes.
- `onPointerMove` returns early unless `stroking && (mode === 'brush' || mode ===
  'erase')` — no painting after the tool switched away.
- `onPointerUp` starts with `if (stroking) { finalizeStroke(); return; }`, then the
  existing `!mode || !photoW` guard and tap handling (tap behavior unchanged —
  `stroking` is only ever set in brush/erase modes).
- `editCanvas.setPointerCapture(evt.pointerId)` wrapped in try/catch: synthetic
  PointerEvents (harness) and inactive pointer ids throw NotFoundError, and the
  stroke logic must not depend on capture succeeding.

**Harness regression added** (`tests/manual/visualizer-harness.html`, placed before
the dispose/re-init block so photo + mask are still alive; all existing assertions
kept): a helper dispatches synthetic `PointerEvent`s on the edit canvas, converting
photo-px to client coords via `photoImg.getBoundingClientRect()` (photo 1200x900).
Scenario: brush mode + size 60, pointerdown+pointermove at (100, 800), then
`setMode(null)` mid-stroke, then pointermove+pointerup at (1100, 500). Both points
verified against the trapezoid geometry to be outside the mask *including the 30px
brush radius*: left edge at y=800 is x ~ 222 (parametrizing (430,400)->(180,880):
t = 400/480, x = 430 - 250t), so the brush disc [70,130] clears it; right edge at
y=500 is x ~ 820 ((760,400)->(1050,880): t = 100/480, x = 760 + 290t), so
[1070,1130] clears it. (The coordinator's note quoted "left edge at y=800 is
x~388" — the correct value is ~222, but the conclusion that (100,800) is outside
holds either way.) Assertions: both points start unselected; after the scenario the
first point IS painted (stroke committed by setMode's finalize) and the second is
NOT (no stray painting after the switch); the whole scenario is wrapped in
try/catch so nothing may throw.

**RED evidence for the regression** (harness run against the pre-fix JS via
`git stash push -- src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js`, then
`git stash pop`):
```
<p id="status" class="fail">FAIL: mid-stroke: no stray painting after tool switch (second point clean)</p>
```
i.e. the new test catches exactly the reported defect (old code kept painting at
the second point). The "first point painted" assertion passes even on old code
(paint lands on pointerdown regardless of commit), so the discriminating assertion
is the second one — and the events demonstrably flow through the real handlers.

**GREEN evidence (both paths, fix in place):**
```
"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --headless=new --disable-gpu-sandbox --virtual-time-budget=5000 --dump-dom "<repo>/tests/manual/visualizer-harness.html"
  -> <title>visualizer.js harness (webgl)</title>
  -> <p id="status" class="pass">ALL PASS — ...</p>

"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --headless=new --disable-gpu-sandbox --virtual-time-budget=8000 --dump-dom "<repo>/tests/manual/visualizer-harness.html?fallback=1"
  -> <title>visualizer.js harness (canvas-2d)</title>
  -> <p id="status" class="pass">ALL PASS — ...</p>
```

**Build:** `dotnet build src/NaturalStoneImpex.Client` — Build succeeded,
0 Warning(s), 0 Error(s).

**Commit:** `755643e` — "fix(visualizer): commit active brush stroke on tool change"
(2 files: visualizer.js, visualizer-harness.html).
