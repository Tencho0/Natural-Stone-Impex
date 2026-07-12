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

  // ---------- lifecycle ----------

  // Reset all non-GL module state so a fresh init() starts from a clean slate.
  // Without this, render()'s guard (!maskPresent || !tileSource || !pxToGround)
  // could pass on stale data from a previous session after dispose()/re-init.
  function resetState() {
    photoW = 0; photoH = 0;
    maskCanvas = null; blurredMask = null; maskPresent = false;
    corners = null; groundToPx = null; pxToGround = null;
    tileSource = null; tileMeters = 1.0; scaleFactor = 1.0; rotationRad = 0;
    lumMean = 0.5;
    // Interaction layer (Task 8): a fresh init() must not carry over an active
    // drawing mode or a mid-stroke flag from a previous session's mask editing.
    mode = null; stroking = false; strokeMoved = false;
  }

  // Release the WebGL context (if any) and drop GPU resource handles.
  // Browsers cap live WebGL contexts (~16); repeated init() without an explicit
  // loseContext() would leak them until getContext('webgl') starts returning null.
  function releaseGl() {
    if (gl) {
      var lose = gl.getExtension('WEBGL_lose_context');
      if (lose) lose.loseContext();
    }
    gl = null; program = null;
    photoTexture = null; maskTexture = null; tileTexture = null;
  }

  // ---------- public API ----------

  var api = {
    init: function (stageId, ref, options) {
      releaseGl();   // Blazor may re-mount without calling dispose(); free the old context
      resetState();
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

    getStageRect: function () {
      var r = photoImg.getBoundingClientRect();
      return { left: r.left, top: r.top, width: r.width, height: r.height };
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
      releaseGl();
      resetState();
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

  // Commit an in-progress brush/erase stroke: rebuild derived mask data,
  // re-render, and notify .NET. Safe to call when no stroke is active.
  function finalizeStroke() {
    if (!stroking) return;
    stroking = false;
    rebuildMaskDerived();
    api.render();
    if (dotNetRef) dotNetRef.invokeMethodAsync('OnMaskEditedAsync');
  }

  function onPointerDown(evt) {
    if (!mode || !photoW) return;
    evt.preventDefault();
    if (mode === 'brush' || mode === 'erase') {
      stroking = true;
      // Synthetic PointerEvents (test harness) and inactive pointer ids throw
      // NotFoundError here; the stroke must not depend on capture succeeding.
      try { editCanvas.setPointerCapture(evt.pointerId); } catch (e) { /* ignore */ }
      var p = eventToPhotoPx(evt);
      paintAt(p.x, p.y);
    }
    strokeMoved = false;
  }

  function onPointerMove(evt) {
    // Paint only while a stroke is live AND the current tool still paints —
    // a mid-stroke setMode() switch must not keep painting with stale semantics.
    if (!stroking || (mode !== 'brush' && mode !== 'erase')) return;
    strokeMoved = true;
    var p = eventToPhotoPx(evt);
    paintAt(p.x, p.y);
  }

  function onPointerUp(evt) {
    // A live stroke always gets committed, even if the tool changed mid-stroke.
    if (stroking) { finalizeStroke(); return; }
    if (!mode || !photoW) return;
    var p = eventToPhotoPx(evt);
    if (mode === 'tap-add' || mode === 'tap-remove') {
      if (dotNetRef)
        dotNetRef.invokeMethodAsync('OnCanvasTapAsync', p.x, p.y, mode === 'tap-add' ? 1 : 0);
    }
  }

  api.setMode = function (m) {
    finalizeStroke(); // switching tools mid-stroke commits the live stroke first
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
    // Note: drawing the (fully opaque) photo here forces the destination alpha to 1
    // across the WHOLE canvas per Porter-Duff compositing rules (alpha_out = alpha_src
    // + alpha_dst * (1 - alpha_src) = 1 when alpha_src = 1), regardless of blend mode
    // or the pre-existing per-pixel alpha from the triangle warp above. So a plain
    // 'destination-in' with blurredMask would NOT clip anything here anyway: the mask
    // canvas is an opaque black/white raster (its red channel *is* the selection
    // signal, same convention as the WebGL shader's u_mask.r read) — not an image with
    // real per-pixel alpha — so compositing against its (uniformly opaque) alpha is a
    // no-op. Clip explicitly below using the mask's red channel as the alpha multiplier.
    pctx.globalCompositeOperation = 'multiply';
    pctx.filter = 'grayscale(1) brightness(' + (1 / Math.max(lumMean, 0.2)).toFixed(2) + ')';
    pctx.drawImage(photoImg, 0, 0, photoW, photoH);
    pctx.filter = 'none';
    pctx.globalCompositeOperation = 'source-over';

    // Clip to the (feathered) mask: use its red channel as the alpha multiplier.
    var maskPixels = blurredMask.getContext('2d').getImageData(0, 0, photoW, photoH);
    var pavedPixels = pctx.getImageData(0, 0, photoW, photoH);
    var md = maskPixels.data, pd = pavedPixels.data;
    for (var i = 0; i < pd.length; i += 4) pd[i + 3] = pd[i + 3] * md[i] / 255;
    pctx.putImageData(pavedPixels, 0, 0);

    ctx.drawImage(pavedLayer, 0, 0);
  };

  return api;
})();
