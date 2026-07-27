### Task 7: visualizer.js — homography + WebGL rendering core

**Files:**
- Create: `src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js`
- Create: `tests/manual/visualizer-harness.html`
- Modify: `src/NaturalStoneImpex.Client/wwwroot/index.html` (script tag before the blazor script)

**Interfaces:**
- Produces `window.nsiVisualizer` with (Tasks 8, 10, 11 depend on these exact names):
  - `init(stageElementId, dotNetRef|null, options|null)` → `{ webgl: boolean }` — builds `<img>` + overlay canvases inside the stage div; `options.forceFallback` for testing.
  - `loadPhotoFromDataUrl(dataUrl)` → Promise `{ width, height }`
  - `setMaskPng(base64Png)` → Promise (draws server mask into the internal mask canvas, rebuilds derived textures)
  - `clearMask()`, `hasMask()` → boolean, `setMaskVisible(bool)` (green tint overlay)
  - `defaultCornersFromMask()` → `[tlx,tly,trx,try,brx,bry,blx,bly]` (photo px), `setCorners(cornersArray)`
  - `setProductTexture(url, widthMeters)` → Promise, `setScale(factor)`, `setRotation(degrees)`
  - `render()`, `setCompareRatio(percent)` (0 = full "after", 100 = full "before")
  - `exportResultDataUrl()` → jpeg data URL, `downloadResult(filename)`
  - `dispose()`
  - Internal test hooks: `_test.computeHomography(src, dst)`, `_test.applyH(h, x, y)` → `[x', y']`
- The homography maps a virtual ground rectangle of 10 m × 15 m (constants `GROUND_W = 10`, `GROUND_H = 15`) onto the 4 corner points (order: top-left, top-right, bottom-right, bottom-left, in photo pixels). Texture tile physical size = `widthMeters × scaleFactor` meters.

- [ ] **Step 1: Write the failing test harness**

Create `tests/manual/visualizer-harness.html` (opened directly from disk with `file://`; everything is procedural so no CORS issues):

```html
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>visualizer.js harness</title>
<style>
  body { font-family: sans-serif; margin: 20px; }
  #stage { position: relative; width: 800px; }
  #status { font-weight: bold; font-size: 1.2em; }
  .pass { color: green; } .fail { color: red; }
  label { display: inline-block; width: 120px; }
</style>
</head>
<body>
<h1>visualizer.js harness</h1>
<p id="status">running…</p>
<div>
  <label>Scale</label><input id="scale" type="range" min="0.3" max="3" step="0.05" value="1">
  <label>Rotation</label><input id="rot" type="range" min="0" max="90" step="1" value="0">
  <label>Compare</label><input id="cmp" type="range" min="0" max="100" step="1" value="0">
</div>
<div id="stage"></div>
<script src="../../src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js"></script>
<script>
(async function () {
  const failures = [];
  const assert = (cond, msg) => { if (!cond) failures.push(msg); };
  const near = (a, b, eps) => Math.abs(a - b) < (eps || 1e-6);

  try {
    const viz = window.nsiVisualizer;
    assert(!!viz, "module loaded");

    // --- homography math ---
    // Identity: unit square -> unit square
    const sq = [0,0, 1,0, 1,1, 0,1];
    const hI = viz._test.computeHomography(sq, sq);
    const p = viz._test.applyH(hI, 0.5, 0.5);
    assert(near(p[0], 0.5) && near(p[1], 0.5), "identity homography");

    // Known projective map: square -> trapezoid; corners must map exactly
    const dst = [100,50, 700,80, 780,560, 40,540];
    const h = viz._test.computeHomography(sq, dst);
    const corners = [[0,0],[1,0],[1,1],[0,1]];
    corners.forEach((c, i) => {
      const q = viz._test.applyH(h, c[0], c[1]);
      assert(near(q[0], dst[i*2], 1e-3) && near(q[1], dst[i*2+1], 1e-3), "corner " + i + " maps exactly");
    });

    // --- rendering (visual) ---
    const forceFallback = new URLSearchParams(location.search).has("fallback");
    const mode = viz.init("stage", null, { forceFallback });
    document.title += forceFallback ? " (canvas-2d)" : (mode.webgl ? " (webgl)" : " (canvas-2d auto)");

    // Procedural photo: sky + green + gray trapezoid "driveway", 1200x900
    const photo = document.createElement("canvas");
    photo.width = 1200; photo.height = 900;
    const pctx = photo.getContext("2d");
    pctx.fillStyle = "#9ec3e6"; pctx.fillRect(0, 0, 1200, 380);
    pctx.fillStyle = "#5e9a4e"; pctx.fillRect(0, 380, 1200, 520);
    pctx.fillStyle = "#8a8a86";
    pctx.beginPath();
    pctx.moveTo(430, 400); pctx.lineTo(760, 400); pctx.lineTo(1050, 880); pctx.lineTo(180, 880);
    pctx.closePath(); pctx.fill();
    // add a dark "shadow" band to verify luminance transfer
    pctx.fillStyle = "rgba(20,20,30,0.45)";
    pctx.fillRect(150, 600, 1050, 120);
    await viz.loadPhotoFromDataUrl(photo.toDataURL("image/png"));

    // Procedural mask = same trapezoid, white on black
    const mask = document.createElement("canvas");
    mask.width = 1200; mask.height = 900;
    const mctx = mask.getContext("2d");
    mctx.fillStyle = "#000"; mctx.fillRect(0, 0, 1200, 900);
    mctx.fillStyle = "#fff";
    mctx.beginPath();
    mctx.moveTo(430, 400); mctx.lineTo(760, 400); mctx.lineTo(1050, 880); mctx.lineTo(180, 880);
    mctx.closePath(); mctx.fill();
    await viz.setMaskPng(mask.toDataURL("image/png").split(",")[1]);
    assert(viz.hasMask(), "mask registered");

    const def = viz.defaultCornersFromMask();
    assert(def.length === 8, "default corners has 8 values");
    assert(def[7] > def[1], "bottom corners below top corners");
    viz.setCorners(def);

    // Procedural stone texture
    const tile = document.createElement("canvas");
    tile.width = 256; tile.height = 256;
    const tctx = tile.getContext("2d");
    tctx.fillStyle = "#b8b0a0"; tctx.fillRect(0, 0, 256, 256);
    tctx.strokeStyle = "#6b6458"; tctx.lineWidth = 6;
    for (let i = 0; i <= 2; i++) {
      tctx.beginPath(); tctx.moveTo(0, i * 128); tctx.lineTo(256, i * 128); tctx.stroke();
      tctx.beginPath(); tctx.moveTo(i * 128, 0); tctx.lineTo(i * 128, 256); tctx.stroke();
    }
    await viz.setProductTexture(tile.toDataURL("image/png"), 1.0);
    viz.render();

    const dataUrl = viz.exportResultDataUrl();
    assert(dataUrl.startsWith("data:image/jpeg"), "export produces jpeg data url");

    document.getElementById("scale").oninput = e => { viz.setScale(parseFloat(e.target.value)); viz.render(); };
    document.getElementById("rot").oninput = e => { viz.setRotation(parseFloat(e.target.value)); viz.render(); };
    document.getElementById("cmp").oninput = e => viz.setCompareRatio(parseFloat(e.target.value));
  } catch (err) {
    failures.push("exception: " + err.message);
    console.error(err);
  }

  const status = document.getElementById("status");
  if (failures.length === 0) {
    status.textContent = "ALL PASS — now verify visually: stones must recede with perspective, shadow band must remain visible on the stones.";
    status.className = "pass";
  } else {
    status.textContent = "FAIL: " + failures.join("; ");
    status.className = "fail";
  }
})();
</script>
</body>
</html>
```

- [ ] **Step 2: Open harness to verify it fails**

Open `tests/manual/visualizer-harness.html` in Chrome (double-click).
Expected: red **FAIL: module loaded** (visualizer.js does not exist yet).

- [ ] **Step 3: Implement the module**

Create `src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js`:

```javascript
// Product visualizer rendering engine. Plain JS, driven from Blazor via JS interop.
// Pipeline: product texture -> homography warp (ground plane) -> mask clip -> luminance transfer.
// WebGL1 primary renderer (true projective mapping); canvas-2D fallback in Task 8.
window.nsiVisualizer = (function () {
  'use strict';

  var GROUND_W = 10;   // meters spanned by the perspective quad, left-right
  var GROUND_H = 15;   // meters spanned near-far (heuristic for a typical tilted photo)

  var stage = null, photoImg = null, glCanvas = null, editCanvas = null;
  var gl = null, program = null, uniforms = {};
  var dotNetRef = null, forceFallback = false;
  var photoW = 0, photoH = 0;
  var maskCanvas = null, blurredMask = null, maskPresent = false;
  var corners = null, groundToPx = null, pxToGround = null;
  var tileSource = null, tileMeters = 1.0, scaleFactor = 1.0, rotationRad = 0;
  var photoTexture = null, maskTexture = null, tileTexture = null, lumMean = 0.5;

  // ---------- linear algebra ----------

  // Solve the 8x8 system for a homography h (h9 = 1) mapping src[i] -> dst[i], 4 point pairs.
  // src/dst are flat arrays [x0,y0, x1,y1, x2,y2, x3,y3]. Returns row-major 9-element array.
  function computeHomography(src, dst) {
    var a = [], b = [];
    for (var i = 0; i < 4; i++) {
      var sx = src[i * 2], sy = src[i * 2 + 1];
      var dx = dst[i * 2], dy = dst[i * 2 + 1];
      a.push([sx, sy, 1, 0, 0, 0, -dx * sx, -dx * sy]); b.push(dx);
      a.push([0, 0, 0, sx, sy, 1, -dy * sx, -dy * sy]); b.push(dy);
    }
    // Gaussian elimination with partial pivoting
    for (var col = 0; col < 8; col++) {
      var pivot = col;
      for (var r = col + 1; r < 8; r++)
        if (Math.abs(a[r][col]) > Math.abs(a[pivot][col])) pivot = r;
      var tmp = a[col]; a[col] = a[pivot]; a[pivot] = tmp;
      var tb = b[col]; b[col] = b[pivot]; b[pivot] = tb;
      for (var row = col + 1; row < 8; row++) {
        var f = a[row][col] / a[col][col];
        for (var k = col; k < 8; k++) a[row][k] -= f * a[col][k];
        b[row] -= f * b[col];
      }
    }
    var h = new Array(8);
    for (var rr = 7; rr >= 0; rr--) {
      var sum = b[rr];
      for (var cc = rr + 1; cc < 8; cc++) sum -= a[rr][cc] * h[cc];
      h[rr] = sum / a[rr][rr];
    }
    return [h[0], h[1], h[2], h[3], h[4], h[5], h[6], h[7], 1];
  }

  function invert3(m) {
    var a = m[0], b = m[1], c = m[2], d = m[3], e = m[4], f = m[5], g = m[6], h = m[7], i = m[8];
    var A = e * i - f * h, B = c * h - b * i, C = b * f - c * e;
    var det = a * A + d * B + g * C;
    return [A / det, B / det, C / det,
            (f * g - d * i) / det, (a * i - c * g) / det, (c * d - a * f) / det,
            (d * h - e * g) / det, (b * g - a * h) / det, (a * e - b * d) / det];
  }

  function applyH(m, x, y) {
    var w = m[6] * x + m[7] * y + m[8];
    return [(m[0] * x + m[1] * y + m[2]) / w, (m[3] * x + m[4] * y + m[5]) / w];
  }

  // ---------- WebGL ----------

  var VS = 'attribute vec2 a_pos; varying vec2 v_uv;' +
    'void main(){ v_uv = a_pos * 0.5 + 0.5; gl_Position = vec4(a_pos, 0.0, 1.0); }';

  var FS = 'precision highp float; varying vec2 v_uv;' +
    'uniform vec2 u_size; uniform sampler2D u_photo; uniform sampler2D u_tile; uniform sampler2D u_mask;' +
    'uniform mat3 u_invH; uniform float u_tileMeters; uniform float u_rot; uniform float u_lumMean;' +
    'void main(){' +
    '  vec2 uv = vec2(v_uv.x, 1.0 - v_uv.y);' +           // top-left origin, matches image rows
    '  float m = texture2D(u_mask, uv).r;' +
    '  if (m < 0.01) { gl_FragColor = vec4(0.0); return; }' +
    '  vec2 px = uv * u_size;' +
    '  vec3 g = u_invH * vec3(px, 1.0);' +
    '  vec2 ground = g.xy / g.z;' +
    '  float c = cos(u_rot); float s = sin(u_rot);' +
    '  ground = mat2(c, -s, s, c) * ground;' +
    '  vec3 stone = texture2D(u_tile, ground / u_tileMeters).rgb;' +
    '  float lum = dot(texture2D(u_photo, uv).rgb, vec3(0.299, 0.587, 0.114));' +
    '  float shade = clamp(lum / max(u_lumMean, 0.05), 0.25, 1.6);' +
    '  gl_FragColor = vec4(stone * shade, m);' +
    '}';

  function compileShader(type, source) {
    var shader = gl.createShader(type);
    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS))
      throw new Error('shader: ' + gl.getShaderInfoLog(shader));
    return shader;
  }

  function initGl() {
    gl = glCanvas.getContext('webgl', { alpha: true, preserveDrawingBuffer: true });
    if (!gl) return false;
    program = gl.createProgram();
    gl.attachShader(program, compileShader(gl.VERTEX_SHADER, VS));
    gl.attachShader(program, compileShader(gl.FRAGMENT_SHADER, FS));
    gl.linkProgram(program);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS))
      throw new Error('link: ' + gl.getProgramInfoLog(program));
    gl.useProgram(program);

    var quad = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, quad);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 1, -1, -1, 1, 1, 1]), gl.STATIC_DRAW);
    var aPos = gl.getAttribLocation(program, 'a_pos');
    gl.enableVertexAttribArray(aPos);
    gl.vertexAttribPointer(aPos, 2, gl.FLOAT, false, 0, 0);

    ['u_size', 'u_photo', 'u_tile', 'u_mask', 'u_invH', 'u_tileMeters', 'u_rot', 'u_lumMean']
      .forEach(function (n) { uniforms[n] = gl.getUniformLocation(program, n); });
    gl.uniform1i(uniforms.u_photo, 0);
    gl.uniform1i(uniforms.u_tile, 1);
    gl.uniform1i(uniforms.u_mask, 2);
    gl.enable(gl.BLEND);
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
    gl.clearColor(0, 0, 0, 0);
    return true;
  }

  function uploadTexture(existing, source, repeat) {
    var texture = existing || gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, source);
    if (repeat) { // requires power-of-two source
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.REPEAT);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.REPEAT);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
      gl.generateMipmap(gl.TEXTURE_2D);
    } else {
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    }
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    return texture;
  }

  // ---------- derived mask data ----------

  function rebuildMaskDerived() {
    blurredMask = document.createElement('canvas');
    blurredMask.width = photoW; blurredMask.height = photoH;
    var ctx = blurredMask.getContext('2d');
    ctx.filter = 'blur(3px)';                 // feathered edge
    ctx.drawImage(maskCanvas, 0, 0);

    // mean luminance of the photo inside the mask (for shadow-preserving shading)
    var w = 128, h = Math.max(1, Math.round(photoH / photoW * 128));
    var ps = document.createElement('canvas'); ps.width = w; ps.height = h;
    ps.getContext('2d').drawImage(photoImg, 0, 0, w, h);
    var ms = document.createElement('canvas'); ms.width = w; ms.height = h;
    ms.getContext('2d').drawImage(maskCanvas, 0, 0, w, h);
    var pd = ps.getContext('2d').getImageData(0, 0, w, h).data;
    var md = ms.getContext('2d').getImageData(0, 0, w, h).data;
    var sum = 0, count = 0;
    for (var i = 0; i < pd.length; i += 4) {
      if (md[i] > 128) {
        sum += (0.299 * pd[i] + 0.587 * pd[i + 1] + 0.114 * pd[i + 2]) / 255;
        count++;
      }
    }
    lumMean = count > 0 ? sum / count : 0.5;
    if (gl) maskTexture = uploadTexture(maskTexture, blurredMask, false);
    drawMaskTint();
  }

  function drawMaskTint() {
    if (!editCanvas) return;
    var ctx = editCanvas.getContext('2d');
    ctx.clearRect(0, 0, photoW, photoH);
    if (!maskPresent || editCanvas.style.display === 'none') return;
    ctx.drawImage(maskCanvas, 0, 0);
    ctx.globalCompositeOperation = 'source-in';
    ctx.fillStyle = 'rgba(40, 200, 90, 0.35)';
    ctx.fillRect(0, 0, photoW, photoH);
    ctx.globalCompositeOperation = 'source-over';
  }

  function maskBBox() {
    var w = 128, h = Math.max(1, Math.round(photoH / photoW * 128));
    var small = document.createElement('canvas'); small.width = w; small.height = h;
    small.getContext('2d').drawImage(maskCanvas, 0, 0, w, h);
    var d = small.getContext('2d').getImageData(0, 0, w, h).data;
    var minX = w, minY = h, maxX = 0, maxY = 0, found = false;
    for (var y = 0; y < h; y++)
      for (var x = 0; x < w; x++)
        if (d[(y * w + x) * 4] > 128) {
          found = true;
          if (x < minX) minX = x; if (x > maxX) maxX = x;
          if (y < minY) minY = y; if (y > maxY) maxY = y;
        }
    if (!found) return { x0: 0.1 * photoW, y0: 0.4 * photoH, x1: 0.9 * photoW, y1: 0.95 * photoH };
    var sx = photoW / w, sy = photoH / h;
    return { x0: minX * sx, y0: minY * sy, x1: (maxX + 1) * sx, y1: (maxY + 1) * sy };
  }

  // ---------- public API ----------

  var api = {
    init: function (stageId, ref, options) {
      stage = document.getElementById(stageId);
      dotNetRef = ref || null;
      forceFallback = !!(options && options.forceFallback);
      stage.style.position = 'relative';
      stage.innerHTML = '';

      photoImg = document.createElement('img');
      photoImg.style.cssText = 'display:block;width:100%;height:auto;user-select:none;-webkit-user-drag:none;';
      glCanvas = document.createElement('canvas');
      glCanvas.style.cssText = 'position:absolute;inset:0;width:100%;height:100%;pointer-events:none;';
      editCanvas = document.createElement('canvas');
      editCanvas.style.cssText = 'position:absolute;inset:0;width:100%;height:100%;touch-action:none;';
      stage.appendChild(photoImg);
      stage.appendChild(glCanvas);
      stage.appendChild(editCanvas);

      var webgl = false;
      if (!forceFallback) {
        try { webgl = initGl(); } catch (e) { console.warn('WebGL unavailable:', e); webgl = false; }
      }
      if (!webgl) gl = null;
      if (api._wireEvents) api._wireEvents(); // installed by the interaction layer (Task 8)
      return { webgl: !!gl };
    },

    loadPhotoFromDataUrl: function (dataUrl) {
      return new Promise(function (resolve, reject) {
        photoImg.onload = function () {
          photoW = photoImg.naturalWidth; photoH = photoImg.naturalHeight;
          glCanvas.width = photoW; glCanvas.height = photoH;
          editCanvas.width = photoW; editCanvas.height = photoH;
          maskCanvas = document.createElement('canvas');
          maskCanvas.width = photoW; maskCanvas.height = photoH;
          maskCanvas.getContext('2d').fillStyle = '#000';
          maskCanvas.getContext('2d').fillRect(0, 0, photoW, photoH);
          maskPresent = false;
          if (gl) {
            gl.viewport(0, 0, photoW, photoH);
            photoTexture = uploadTexture(photoTexture, photoImg, false);
            gl.clear(gl.COLOR_BUFFER_BIT);
          }
          resolve({ width: photoW, height: photoH });
        };
        photoImg.onerror = reject;
        photoImg.crossOrigin = 'anonymous';
        photoImg.src = dataUrl;
      });
    },

    setMaskPng: function (base64) {
      return new Promise(function (resolve, reject) {
        var img = new Image();
        img.onload = function () {
          var ctx = maskCanvas.getContext('2d');
          ctx.fillStyle = '#000';
          ctx.fillRect(0, 0, photoW, photoH);
          ctx.drawImage(img, 0, 0, photoW, photoH);
          maskPresent = true;
          rebuildMaskDerived();
          resolve();
        };
        img.onerror = reject;
        img.src = 'data:image/png;base64,' + base64;
      });
    },

    clearMask: function () {
      var ctx = maskCanvas.getContext('2d');
      ctx.fillStyle = '#000';
      ctx.fillRect(0, 0, photoW, photoH);
      maskPresent = false;
      rebuildMaskDerived();
      if (gl) gl.clear(gl.COLOR_BUFFER_BIT);
    },

    hasMask: function () { return maskPresent; },

    setMaskVisible: function (visible) {
      editCanvas.style.display = visible ? 'block' : 'none';
      drawMaskTint();
    },

    defaultCornersFromMask: function () {
      var box = maskBBox();
      var cx = (box.x0 + box.x1) / 2;
      var halfTop = (box.x1 - box.x0) * 0.45 / 2; // spec: top edge ~45% of bottom width
      return [cx - halfTop, box.y0, cx + halfTop, box.y0, box.x1, box.y1, box.x0, box.y1];
    },

    setCorners: function (c) {
      corners = c.slice();
      var srcGround = [0, 0, GROUND_W, 0, GROUND_W, GROUND_H, 0, GROUND_H];
      groundToPx = computeHomography(srcGround, corners);
      pxToGround = invert3(groundToPx);
    },

    setProductTexture: function (url, widthMeters) {
      tileMeters = widthMeters || 1.0;
      return new Promise(function (resolve, reject) {
        var img = new Image();
        img.crossOrigin = 'anonymous';
        img.onload = function () {
          // Resize to power-of-two so WebGL REPEAT + mipmaps work.
          var pot = document.createElement('canvas');
          pot.width = 1024; pot.height = 1024;
          pot.getContext('2d').drawImage(img, 0, 0, 1024, 1024);
          tileSource = pot;
          if (gl) tileTexture = uploadTexture(tileTexture, pot, true);
          resolve();
        };
        img.onerror = reject;
        img.src = url;
      });
    },

    setScale: function (f) { scaleFactor = f; },
    setRotation: function (deg) { rotationRad = deg * Math.PI / 180; },

    render: function () {
      if (!maskPresent || !tileSource || !pxToGround) return;
      if (!gl) { api._renderFallback(); return; } // Task 8
      gl.clear(gl.COLOR_BUFFER_BIT);
      gl.uniform2f(uniforms.u_size, photoW, photoH);
      // row-major -> column-major for uniformMatrix3fv
      var m = pxToGround;
      gl.uniformMatrix3fv(uniforms.u_invH, false,
        [m[0], m[3], m[6], m[1], m[4], m[7], m[2], m[5], m[8]]);
      gl.uniform1f(uniforms.u_tileMeters, tileMeters * scaleFactor);
      gl.uniform1f(uniforms.u_rot, rotationRad);
      gl.uniform1f(uniforms.u_lumMean, lumMean);
      gl.activeTexture(gl.TEXTURE0); gl.bindTexture(gl.TEXTURE_2D, photoTexture);
      gl.activeTexture(gl.TEXTURE1); gl.bindTexture(gl.TEXTURE_2D, tileTexture);
      gl.activeTexture(gl.TEXTURE2); gl.bindTexture(gl.TEXTURE_2D, maskTexture);
      gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
    },

    setCompareRatio: function (percent) {
      glCanvas.style.clipPath = 'inset(0 0 0 ' + percent + '%)';
    },

    exportResultDataUrl: function () {
      var out = document.createElement('canvas');
      out.width = photoW; out.height = photoH;
      var ctx = out.getContext('2d');
      ctx.drawImage(photoImg, 0, 0, photoW, photoH);
      ctx.drawImage(glCanvas, 0, 0, photoW, photoH);
      return out.toDataURL('image/jpeg', 0.92);
    },

    downloadResult: function (filename) {
      var link = document.createElement('a');
      link.download = filename;
      link.href = api.exportResultDataUrl();
      link.click();
    },

    dispose: function () {
      dotNetRef = null;
      if (stage) stage.innerHTML = '';
      gl = null; photoTexture = null; maskTexture = null; tileTexture = null;
    },

    _test: { computeHomography: computeHomography, applyH: applyH, invert3: invert3 },
    _internal: function () {
      return {
        get maskCanvas() { return maskCanvas; },
        get photoImg() { return photoImg; },
        get glCanvas() { return glCanvas; },
        get editCanvas() { return editCanvas; },
        get groundToPx() { return groundToPx; },
        get dotNetRef() { return dotNetRef; },
        get size() { return { w: photoW, h: photoH }; },
        get tile() { return { source: tileSource, meters: tileMeters, scale: scaleFactor, rot: rotationRad }; },
        get lumMean() { return lumMean; },
        get blurredMask() { return blurredMask; },
        setMaskPresent: function (v) { maskPresent = v; },
        rebuildMaskDerived: rebuildMaskDerived,
        GROUND_W: GROUND_W, GROUND_H: GROUND_H
      };
    }
  };

  return api;
})();
```

- [ ] **Step 4: Open harness to verify it passes**

Open `tests/manual/visualizer-harness.html` in Chrome.
Expected: green **ALL PASS** plus a rendered image where the gray trapezoid is covered with the grid-stone texture, the pattern gets smaller toward the top (perspective), and the dark shadow band is visible **on** the stones (luminance transfer). Move the three sliders: scale/rotation re-render correctly; compare reveals the original from the left.

- [ ] **Step 5: Register the script in the Blazor client**

In `src/NaturalStoneImpex.Client/wwwroot/index.html`, before the `<script src="_framework/blazor.webassembly.js"></script>` line add:

```html
    <script src="js/visualizer.js"></script>
```

Run: `dotnet build`
Expected: success.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): WebGL rendering engine with homography and luminance transfer"
```

---

