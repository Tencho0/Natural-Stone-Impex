### Task 8: visualizer.js — taps, brush editing, canvas-2D fallback

**Files:**
- Modify: `src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js`
- Modify: `tests/manual/visualizer-harness.html`

**Interfaces:**
- Consumes: `_internal()` accessors from Task 7.
- Produces (Tasks 10–11 depend on these):
  - `setMode(mode)` — `'tap-add' | 'tap-remove' | 'brush' | 'erase' | null`
  - `setBrushSize(px)` — brush diameter in photo pixels (default 40)
  - Tap modes invoke `dotNetRef.invokeMethodAsync('OnCanvasTapAsync', x, y, label)` with photo-pixel coordinates (label 1 for `tap-add`, 0 for `tap-remove`).
  - Brush/erase strokes edit the mask locally; on stroke end the module rebuilds derived data, re-renders, and invokes `dotNetRef.invokeMethodAsync('OnMaskEditedAsync')`.
  - `api._renderFallback()` — canvas-2D renderer used automatically when WebGL is unavailable (or `forceFallback`).

- [ ] **Step 1: Extend the harness with interaction checks**

In `tests/manual/visualizer-harness.html`, inside the async function after the `exportResultDataUrl` assertion, add:

```javascript
    // --- interaction layer ---
    assert(typeof viz.setMode === "function", "setMode exists");
    assert(typeof viz.setBrushSize === "function", "setBrushSize exists");
    // brush stroke programmatically: paint a square patch and verify the mask grew
    const internals = viz._internal();
    const before = internals.maskCanvas.getContext("2d")
      .getImageData(60, 420, 1, 1).data[0];
    viz.setMode("brush");
    viz.setBrushSize(60);
    viz._test.strokeForTest(60, 420);
    const after = internals.maskCanvas.getContext("2d")
      .getImageData(60, 420, 1, 1).data[0];
    assert(before < 128 && after > 128, "brush paints the mask");
    viz.setMode(null);
```

Reload the harness. Expected: red **FAIL: setMode exists** (not implemented yet).

- [ ] **Step 2: Implement interaction + fallback renderer**

In `src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js`, add inside the module (before `return api;`) — a self-contained extension that uses `api._internal()`:

```javascript
  // ---------- interaction layer ----------

  var mode = null, brushSize = 40, stroking = false, strokeMoved = false;

  function eventToPhotoPx(evt) {
    var rect = photoImg.getBoundingClientRect();
    return {
      x: (evt.clientX - rect.left) / rect.width * photoW,
      y: (evt.clientY - rect.top) / rect.height * photoH
    };
  }

  function paintAt(x, y) {
    var ctx = maskCanvas.getContext('2d');
    ctx.globalCompositeOperation = mode === 'erase' ? 'destination-out' : 'source-over';
    ctx.fillStyle = '#fff';
    ctx.beginPath();
    ctx.arc(x, y, brushSize / 2, 0, Math.PI * 2);
    ctx.fill();
    ctx.globalCompositeOperation = 'source-over';
    maskPresent = true;
    drawMaskTint();
  }

  function onPointerDown(evt) {
    if (!mode || !photoW) return;
    evt.preventDefault();
    if (mode === 'brush' || mode === 'erase') {
      stroking = true;
      editCanvas.setPointerCapture(evt.pointerId);
      var p = eventToPhotoPx(evt);
      paintAt(p.x, p.y);
    }
    strokeMoved = false;
  }

  function onPointerMove(evt) {
    if (!stroking) return;
    strokeMoved = true;
    var p = eventToPhotoPx(evt);
    paintAt(p.x, p.y);
  }

  function onPointerUp(evt) {
    if (!mode || !photoW) return;
    var p = eventToPhotoPx(evt);
    if (mode === 'tap-add' || mode === 'tap-remove') {
      if (dotNetRef)
        dotNetRef.invokeMethodAsync('OnCanvasTapAsync', p.x, p.y, mode === 'tap-add' ? 1 : 0);
    } else if (stroking) {
      stroking = false;
      rebuildMaskDerived();
      api.render();
      if (dotNetRef) dotNetRef.invokeMethodAsync('OnMaskEditedAsync');
    }
  }

  api.setMode = function (m) {
    mode = m;
    editCanvas.style.cursor = (m === 'brush' || m === 'erase') ? 'crosshair'
      : (m ? 'pointer' : 'default');
  };
  api.setBrushSize = function (px) { brushSize = px; };
  api._wireEvents = function () {
    editCanvas.addEventListener('pointerdown', onPointerDown);
    editCanvas.addEventListener('pointermove', onPointerMove);
    editCanvas.addEventListener('pointerup', onPointerUp);
  };
  api._test.strokeForTest = function (x, y) { // deterministic brush for the harness
    paintAt(x, y);
    rebuildMaskDerived();
  };

  // ---------- canvas-2D fallback renderer ----------

  // Affine-draw img triangle (sx, sy)[3] onto ctx triangle (dx, dy)[3].
  function drawTriangle(ctx, img, s, d) {
    ctx.save();
    ctx.beginPath();
    ctx.moveTo(d[0], d[1]); ctx.lineTo(d[2], d[3]); ctx.lineTo(d[4], d[5]);
    ctx.closePath();
    ctx.clip();
    var denom = s[0] * (s[5] - s[3]) - s[2] * s[5] + s[4] * s[3] + (s[2] - s[4]) * s[1];
    var m11 = -(s[1] * (d[4] - d[2]) - s[3] * d[4] + s[5] * d[2] + (s[3] - s[5]) * d[0]) / denom;
    var m12 = (s[3] * d[5] + s[1] * (d[3] - d[5]) - s[5] * d[3] + (s[5] - s[3]) * d[1]) / denom;
    var m21 = (s[0] * (d[4] - d[2]) - s[2] * d[4] + s[4] * d[2] + (s[2] - s[4]) * d[0]) / denom;
    var m22 = -(s[2] * d[5] + s[0] * (d[3] - d[5]) - s[4] * d[3] + (s[4] - s[2]) * d[1]) / denom;
    var dx = (s[0] * (s[5] * d[2] - s[3] * d[4]) + s[1] * (s[2] * d[4] - s[4] * d[2]) + (s[3] * s[4] - s[2] * s[5]) * d[0]) / denom;
    var dy = (s[0] * (s[5] * d[3] - s[3] * d[5]) + s[1] * (s[2] * d[5] - s[4] * d[3]) + (s[3] * s[4] - s[2] * s[5]) * d[1]) / denom;
    ctx.transform(m11, m12, m21, m22, dx, dy);
    ctx.drawImage(img, 0, 0);
    ctx.restore();
  }

  api._renderFallback = function () {
    var ctx = glCanvas.getContext('2d');
    if (!ctx) return;
    ctx.clearRect(0, 0, photoW, photoH);

    // Big ground-space texture canvas: whole quad area, tiled + rotated pattern.
    var metersPerTile = tileMeters * scaleFactor;
    var ppm = Math.min(1024 / metersPerTile, 2048 / GROUND_W); // cap resolution
    var big = document.createElement('canvas');
    big.width = Math.round(GROUND_W * ppm);
    big.height = Math.round(GROUND_H * ppm);
    var bctx = big.getContext('2d');
    var tilePx = Math.max(8, Math.round(metersPerTile * ppm));
    var tileScaled = document.createElement('canvas');
    tileScaled.width = tilePx; tileScaled.height = tilePx;
    tileScaled.getContext('2d').drawImage(tileSource, 0, 0, tilePx, tilePx);
    bctx.save();
    bctx.translate(big.width / 2, big.height / 2);
    bctx.rotate(rotationRad);
    bctx.fillStyle = bctx.createPattern(tileScaled, 'repeat');
    var diag = Math.hypot(big.width, big.height);
    bctx.fillRect(-diag, -diag, diag * 2, diag * 2);
    bctx.restore();

    // Warp big canvas onto the photo through the homography, cell by cell (2 triangles each).
    var pavedLayer = document.createElement('canvas');
    pavedLayer.width = photoW; pavedLayer.height = photoH;
    var pctx = pavedLayer.getContext('2d');
    var cells = 12;
    for (var gy = 0; gy < cells; gy++) {
      for (var gx = 0; gx < cells; gx++) {
        var gx0 = gx / cells * GROUND_W, gx1 = (gx + 1) / cells * GROUND_W;
        var gy0 = gy / cells * GROUND_H, gy1 = (gy + 1) / cells * GROUND_H;
        var p00 = applyH(groundToPx, gx0, gy0), p10 = applyH(groundToPx, gx1, gy0);
        var p11 = applyH(groundToPx, gx1, gy1), p01 = applyH(groundToPx, gx0, gy1);
        var sx0 = gx0 * ppm, sx1 = gx1 * ppm, sy0 = gy0 * ppm, sy1 = gy1 * ppm;
        drawTriangle(pctx, big, [sx0, sy0, sx1, sy0, sx1, sy1],
          [p00[0], p00[1], p10[0], p10[1], p11[0], p11[1]]);
        drawTriangle(pctx, big, [sx0, sy0, sx1, sy1, sx0, sy1],
          [p00[0], p00[1], p11[0], p11[1], p01[0], p01[1]]);
      }
    }

    // Luminance transfer (approximate): multiply by brightened grayscale photo.
    pctx.globalCompositeOperation = 'multiply';
    pctx.filter = 'grayscale(1) brightness(' + (1 / Math.max(lumMean, 0.2)).toFixed(2) + ')';
    pctx.drawImage(photoImg, 0, 0, photoW, photoH);
    pctx.filter = 'none';
    // Clip to the (feathered) mask.
    pctx.globalCompositeOperation = 'destination-in';
    pctx.drawImage(blurredMask, 0, 0);
    pctx.globalCompositeOperation = 'source-over';

    ctx.drawImage(pavedLayer, 0, 0);
  };
```

Note: `mode`, `brushSize` etc. live in the same closure, so the Task 7 variables (`photoImg`, `maskCanvas`, `editCanvas`, `photoW`, `photoH`, `maskPresent`, `drawMaskTint`, `rebuildMaskDerived`, `groundToPx`, `tileSource`, `tileMeters`, `scaleFactor`, `rotationRad`, `lumMean`, `blurredMask`, `dotNetRef`) are directly accessible — place this code inside the IIFE, after the `api` object is defined and before `return api;`. Remove `api._internal` usage in the harness if you prefer, but keep `_internal` itself (it is used by the harness assertions).

- [ ] **Step 3: Verify both render paths in the harness**

1. Open `tests/manual/visualizer-harness.html` → Expected: green **ALL PASS** (WebGL path + brush assertion).
2. Open `tests/manual/visualizer-harness.html?fallback=1` → Expected: green **ALL PASS**, title suffix `(canvas-2d)`, and a visually similar (slightly softer) rendering.

- [ ] **Step 4: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): mask editing tools and canvas-2d fallback renderer"
```

---

