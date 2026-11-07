// Product visualizer rendering engine. Plain JS, driven from Blazor via JS interop.
//
// Pipeline per render:
//   product texture (illumination-flattened, made tileable)
//   -> ground-plane homography (camera model auto-initialised from the mask, editable via 4 corners)
//   -> clip to the (feathered) mask
//   -> low-frequency lighting transfer (shadows of the original photo, NOT its surface texture)
//
// Conventions:
// - maskCanvas is an ALPHA mask at photo resolution: selected = opaque white, unselected = transparent.
// - Ground coordinates are metres. groundToPx maps the ground rectangle [0,groundW]x[0,groundD]
//   onto the 4 corners [far-left, far-right, near-right, near-left] (photo pixels).
window.nsiVisualizer = (function () {
  'use strict';

  // Camera assumptions used for the automatic perspective (typical phone photo of the ground).
  var CAMERA_HEIGHT_M = 1.6;       // phone held at chest/eye height
  var FOCAL_PER_LONG_SIDE = 0.7;   // ~26 mm-equivalent main camera
  var SHADE_RANGE = 1.5;           // shade map stores shade / SHADE_RANGE in [0,1]
  var SHADE_LONG_SIDE = 384;       // resolution of the low-frequency lighting map
  var TEXTURE_CACHE_MAX = 12;

  var stage = null, photoImg = null, glCanvas = null, editCanvas = null, gridCanvas = null, divider = null;
  var gl = null, program = null, uniforms = {};
  var dotNetRef = null, forceFallback = false;
  var photoW = 0, photoH = 0;
  var maskCanvas = null, softMask = null, maskPresent = false, maskVisible = true;
  // maskCanvas = samCanvas (last server mask) + addLayer (brush) - eraseLayer (eraser), so manual
  // corrections survive later taps, which replace the server mask.
  var samCanvas = null, addLayer = null, eraseLayer = null;
  var shadeCanvas = null, shadeInfo = null;
  var corners = null, groundW = 10, groundD = 15, groundToPx = null, pxToGround = null;
  var gridVisible = false;
  var tileSource = null, tileAspect = 1, tileMeters = 1.0, scaleFactor = 1.0, rotationRad = 0;
  var currentTextureKey = null, textureCache = [];
  var maskTexture = null, tileTexture = null, shadeTexture = null;

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

  // ---------- small raster helpers ----------

  function makeCanvas(w, h) {
    var c = document.createElement('canvas');
    c.width = Math.max(1, Math.round(w)); c.height = Math.max(1, Math.round(h));
    return c;
  }

  // Downscale by repeated halving (bilinear at each step) so fine detail is averaged, not aliased.
  function downscale(source, srcW, srcH, dstW, dstH) {
    var cur = source, cw = srcW, ch = srcH;
    while (cw / 2 >= dstW * 1.0001 && ch / 2 >= dstH * 1.0001) {
      var next = makeCanvas(Math.round(cw / 2), Math.round(ch / 2));
      var nctx = next.getContext('2d');
      nctx.imageSmoothingEnabled = true;
      nctx.drawImage(cur, 0, 0, cw, ch, 0, 0, next.width, next.height);
      cur = next; cw = next.width; ch = next.height;
    }
    var out = makeCanvas(dstW, dstH);
    var octx = out.getContext('2d');
    octx.imageSmoothingEnabled = true;
    octx.drawImage(cur, 0, 0, cw, ch, 0, 0, out.width, out.height);
    return out;
  }

  // Separable box blur with edge replication; 3 passes approximate a Gaussian (sigma ~ radius).
  function boxBlur(src, w, h, radius, passes) {
    if (radius < 1) return src;
    var a = Float32Array.from(src), b = new Float32Array(w * h);
    var norm = 1 / (2 * radius + 1);
    for (var p = 0; p < (passes || 3); p++) {
      for (var y = 0; y < h; y++) {
        var row = y * w, acc = 0;
        for (var k = -radius; k <= radius; k++) acc += a[row + Math.min(w - 1, Math.max(0, k))];
        for (var x = 0; x < w; x++) {
          b[row + x] = acc * norm;
          acc += a[row + Math.min(w - 1, x + radius + 1)] - a[row + Math.max(0, x - radius)];
        }
      }
      for (var x2 = 0; x2 < w; x2++) {
        var acc2 = 0;
        for (var k2 = -radius; k2 <= radius; k2++) acc2 += b[Math.min(h - 1, Math.max(0, k2)) * w + x2];
        for (var y2 = 0; y2 < h; y2++) {
          a[y2 * w + x2] = acc2 * norm;
          acc2 += b[Math.min(h - 1, y2 + radius + 1) * w + x2] - b[Math.max(0, y2 - radius) * w + x2];
        }
      }
    }
    return a;
  }

  function smoothstep(e0, e1, x) {
    var t = Math.min(1, Math.max(0, (x - e0) / (e1 - e0)));
    return t * t * (3 - 2 * t);
  }

  // ---------- WebGL ----------

  var VS = 'attribute vec2 a_pos; varying vec2 v_uv;' +
    'void main(){ v_uv = a_pos * 0.5 + 0.5; gl_Position = vec4(a_pos, 0.0, 1.0); }';

  // Anti-repetition ("texture repetition" technique by I. Quilez): a smooth noise picks, per
  // region of ground, one of 8 random offsets into the tile; neighbouring regions blend over a
  // narrow band. Breaks the visible lattice of a repeated photo. Needs explicit gradients so the
  // offset jumps don't select the wrong mip level -> only with EXT_shader_texture_lod.
  var FS_HEADER_LOD = '#extension GL_EXT_shader_texture_lod : enable\n' +
    '#extension GL_OES_standard_derivatives : enable\n#define HAS_LOD 1\n';

  var FS =
    '#ifdef GL_FRAGMENT_PRECISION_HIGH\nprecision highp float;\n#else\nprecision mediump float;\n#endif\n' +
    'varying vec2 v_uv;' +
    'uniform vec2 u_size; uniform sampler2D u_tile; uniform sampler2D u_mask; uniform sampler2D u_shade;' +
    'uniform mat3 u_invH; uniform vec2 u_tileSize; uniform float u_rot; uniform float u_shadeRange;' +
    'float hash(vec2 p){ return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }' +
    'float vnoise(vec2 p){ vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);' +
    '  return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y); }' +
    'uniform float u_stochastic;' +
    'vec3 stoneAt(vec2 t){\n' +                            // t = ground position in tile units
    '#ifdef HAS_LOD\n' +
    '  vec2 dx = dFdx(t), dy = dFdy(t);' +
    '  if (u_stochastic < 0.5) return texture2DGradEXT(u_tile, t, dx, dy).rgb;' + // prepared texture: exact

    '  float k = vnoise(t * 0.45) * 0.75 + vnoise(t * 1.1 + 31.0) * 0.25;' +
    '  float l = k * 6.0; float f = fract(l); float ia = floor(l); float ib = ia + 1.0;' +
    '  vec2 offa = sin(vec2(3.0, 7.0) * ia); vec2 offb = sin(vec2(3.0, 7.0) * ib);' +
    '  vec3 ca = texture2DGradEXT(u_tile, t + offa, dx, dy).rgb;' +
    '  vec3 cb = texture2DGradEXT(u_tile, t + offb, dx, dy).rgb;' +
    '  vec3 d = ca - cb;' +
    '  return mix(ca, cb, smoothstep(0.3, 0.7, f - 0.1 * (d.x + d.y + d.z)));\n' +
    '#else\n' +
    '  return texture2D(u_tile, t).rgb;\n' +
    '#endif\n' +
    '}' +
    // No early returns: screen-space derivatives (dFdx, implicit mip selection) must be
    // evaluated in uniform control flow, so coverage is applied at the end instead.
    'void main(){' +
    '  vec2 uv = vec2(v_uv.x, 1.0 - v_uv.y);' +           // top-left origin, matches image rows
    '  vec2 px1 = 1.0 / u_size;' +                        // ~1.5 px feather: 5-tap mask average
    '  float m = 0.4 * texture2D(u_mask, uv).a' +
    '    + 0.15 * (texture2D(u_mask, uv + vec2(1.5 * px1.x, 0.0)).a + texture2D(u_mask, uv - vec2(1.5 * px1.x, 0.0)).a' +
    '    + texture2D(u_mask, uv + vec2(0.0, 1.5 * px1.y)).a + texture2D(u_mask, uv - vec2(0.0, 1.5 * px1.y)).a);' +
    '  vec3 g = u_invH * vec3(uv * u_size, 1.0);' +
    '  m *= step(1e-7, g.z);' +                              // at/above the horizon: no ground
    '  vec2 ground = g.xy / max(g.z, 1e-7);' +
    '  float c = cos(u_rot); float s = sin(u_rot);' +
    '  ground = mat2(c, -s, s, c) * ground;' +
    '  vec3 stone = stoneAt(ground / u_tileSize);' +
    // gentle large-scale variation so repeats of the same tile are not identical
    '  float n = vnoise(ground / (u_tileSize.x * 3.3)) + 0.5 * vnoise(ground / (u_tileSize.x * 1.3) + 17.0);' +
    '  stone *= 0.93 + 0.093 * n;' +
    '  float shade = texture2D(u_shade, uv).r * u_shadeRange;' +
    '  vec3 lin = pow(max(stone, vec3(0.0)), vec3(2.2)) * shade;' + // light in linear space
    '  vec3 col = pow(clamp(lin, 0.0, 1.0), vec3(1.0 / 2.2));' +
    '  gl_FragColor = vec4(col * m, m);' +                  // premultiplied output (no blending needed)
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
    gl = glCanvas.getContext('webgl', { alpha: true, premultipliedAlpha: true, preserveDrawingBuffer: true });
    if (!gl) return false;
    var hasLod = !!gl.getExtension('EXT_shader_texture_lod') && !!gl.getExtension('OES_standard_derivatives');
    program = gl.createProgram();
    gl.attachShader(program, compileShader(gl.VERTEX_SHADER, VS));
    gl.attachShader(program, compileShader(gl.FRAGMENT_SHADER, (hasLod ? FS_HEADER_LOD : '') + FS));
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

    ['u_size', 'u_tile', 'u_mask', 'u_shade', 'u_invH', 'u_tileSize', 'u_rot', 'u_shadeRange', 'u_stochastic']
      .forEach(function (n) { uniforms[n] = gl.getUniformLocation(program, n); });
    gl.uniform1i(uniforms.u_tile, 0);
    gl.uniform1i(uniforms.u_mask, 1);
    gl.uniform1i(uniforms.u_shade, 2);
    gl.disable(gl.BLEND); // single full-screen pass writes premultiplied colour directly
    gl.clearColor(0, 0, 0, 0);
    return true;
  }

  function uploadTexture(existing, source, repeat) {
    var texture = existing || gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
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

  // ---------- product texture preparation ----------

  // Remove the photo's low-frequency lighting (vignetting, a darker corner) so tiles join without
  // visible brightness steps. Keeps the stones' own relief and colour.
  function flattenIllumination(data, w, h) {
    var cw = 6, ch = Math.max(2, Math.round(6 * h / w));
    var src = makeCanvas(w, h);
    src.getContext('2d').putImageData(new ImageData(data, w, h), 0, 0);
    var tiny = downscale(src, w, h, cw, ch);
    var field = makeCanvas(w, h);
    var fctx = field.getContext('2d');
    fctx.imageSmoothingEnabled = true;
    fctx.drawImage(tiny, 0, 0, w, h);
    var f = fctx.getImageData(0, 0, w, h).data;
    var mean = 0, i;
    for (i = 0; i < f.length; i += 4) mean += f[i] * 0.299 + f[i + 1] * 0.587 + f[i + 2] * 0.114;
    mean /= (f.length / 4);
    for (i = 0; i < data.length; i += 4) {
      var l = Math.max(8, f[i] * 0.299 + f[i + 1] * 0.587 + f[i + 2] * 0.114);
      var k = Math.min(1.6, Math.max(0.6, mean / l));
      data[i] = Math.min(255, data[i] * k);
      data[i + 1] = Math.min(255, data[i + 1] * k);
      data[i + 2] = Math.min(255, data[i + 2] * k);
    }
  }

  // Make an image tileable along X with an image-quilting min-cut seam: the right overlap band is
  // wrapped over the left one and the cut follows the path of least colour difference (typically
  // the mortar joints between stones), with a 2 px feather. Returns { data, w }.
  // cyclic: the image is already tileable along Y (rows 0 and h-1 are neighbours), so the cut
  // must start and end at the same column or the Y wrap breaks where the two ends differ.
  function quiltWrapX(data, w, h, overlapFrac, cyclic) {
    var ow = Math.max(6, Math.round(w * overlapFrac));
    var nw = w - ow;
    var cost = new Float32Array(ow * h), x, y, k;
    for (y = 0; y < h; y++)
      for (x = 0; x < ow; x++) {
        var p = (y * w + x) * 4, q = (y * w + nw + x) * 4;
        var dr = data[p] - data[q], dg = data[p + 1] - data[q + 1], db = data[p + 2] - data[q + 2];
        cost[y * ow + x] = dr * dr + dg * dg + db * db;
      }
    var lo = 2, hi = ow - 3; // keep the cut off the band edges so the wrap stays continuous
    var acc = new Float32Array(ow * h), from = new Int16Array(ow * h);
    // Dynamic programming over rows; start = -1 lets the path start anywhere in [lo, hi].
    function solve(start) {
      for (x = 0; x < ow; x++)
        acc[x] = (x < lo || x > hi || (start >= 0 && x !== start)) ? 1e30 : cost[x];
      for (y = 1; y < h; y++)
        for (x = 0; x < ow; x++) {
          if (x < lo || x > hi) { acc[y * ow + x] = 1e30; continue; }
          var best = 1e31, bx = x;
          for (k = -1; k <= 1; k++) {
            var xx = x + k;
            if (xx < 0 || xx >= ow) continue;
            var v = acc[(y - 1) * ow + xx];
            if (v < best) { best = v; bx = xx; }
          }
          acc[y * ow + x] = best + cost[y * ow + x];
          from[y * ow + x] = bx;
        }
    }
    function trace(endX) {
      var s = new Int16Array(h), cx = endX;
      for (y = h - 1; y >= 0; y--) { s[y] = cx; cx = from[y * ow + cx]; }
      return s;
    }
    var seam;
    if (!cyclic) {
      solve(-1);
      var sx = lo, sb = 1e31;
      for (x = lo; x <= hi; x++) if (acc[(h - 1) * ow + x] < sb) { sb = acc[(h - 1) * ow + x]; sx = x; }
      seam = trace(sx);
    } else {
      var bestCost = 1e31, bestStart = lo, step = Math.max(1, Math.round((hi - lo) / 12));
      for (var st = lo; st <= hi; st += step) {
        solve(st);
        var c = acc[(h - 1) * ow + st];
        if (c < bestCost) { bestCost = c; bestStart = st; }
      }
      solve(bestStart);
      seam = trace(bestStart);   // ends where it starts -> rows 0 and h-1 stay consistent
    }

    var out = new Uint8ClampedArray(nw * h * 4);
    for (y = 0; y < h; y++)
      for (x = 0; x < nw; x++) {
        var o = (y * nw + x) * 4, a = (y * w + x) * 4;
        if (x < ow) {
          var t = Math.min(1, Math.max(0, (x - seam[y] + 2) / 4)); // 0 -> wrapped band, 1 -> original
          var bIdx = (y * w + nw + x) * 4;
          out[o] = data[bIdx] + (data[a] - data[bIdx]) * t;
          out[o + 1] = data[bIdx + 1] + (data[a + 1] - data[bIdx + 1]) * t;
          out[o + 2] = data[bIdx + 2] + (data[a + 2] - data[bIdx + 2]) * t;
        } else {
          out[o] = data[a]; out[o + 1] = data[a + 1]; out[o + 2] = data[a + 2];
        }
        out[o + 3] = 255;
      }
    return { data: out, w: nw };
  }

  function transpose(data, w, h) {
    var out = new Uint8ClampedArray(w * h * 4);
    for (var y = 0; y < h; y++)
      for (var x = 0; x < w; x++) {
        var s = (y * w + x) * 4, d = (x * h + y) * 4;
        out[d] = data[s]; out[d + 1] = data[s + 1]; out[d + 2] = data[s + 2]; out[d + 3] = data[s + 3];
      }
    return out;
  }

  // RGBA pixels -> tileable on both axes. Returns { data, w, h } (smaller by the overlap bands).
  function makeTileable(data, w, h) {
    var qx = quiltWrapX(data, w, h, 0.16);                        // seamless left-right
    var tq = transpose(qx.data, qx.w, h);
    var qy = quiltWrapX(tq, h, qx.w, 0.16, true);                 // seamless top-bottom, keeps X wrap
    return { data: transpose(qy.data, qy.w, qx.w), w: qx.w, h: qy.w };
  }

  // Product image -> GPU tile. Returns { canvas (1024 POT), aspect, widthFrac, seamless }.
  // prepared = a dedicated visualizer texture uploaded by the admin (spec §7.1: seamless, evenly
  // lit, top-down). It is used exactly as supplied: no flattening, no re-quilting, and no random
  // offsets in the shader, so a regular laying pattern keeps straight, continuous joints.
  // Otherwise the image is a catalog photo and gets the automatic repair.
  function prepareTile(img, prepared) {
    var maxSide = 1024;
    var s = Math.min(1, maxSide / Math.max(img.naturalWidth, img.naturalHeight));
    var w = Math.max(16, Math.round(img.naturalWidth * s)), h = Math.max(16, Math.round(img.naturalHeight * s));
    var c = makeCanvas(w, h);
    var ctx = c.getContext('2d');
    ctx.drawImage(img, 0, 0, w, h);
    var data = ctx.getImageData(0, 0, w, h).data;
    var seamless = !!prepared;
    var t = { data: data, w: w, h: h };
    if (!seamless) {
      flattenIllumination(data, w, h);
      t = makeTileable(data, w, h);
    }
    var finalW = t.w, finalH = t.h;
    var tileCanvas = makeCanvas(finalW, finalH);
    tileCanvas.getContext('2d').putImageData(new ImageData(t.data, finalW, finalH), 0, 0);
    // Power-of-two copy for WebGL REPEAT + mipmaps; the shader restores the aspect ratio.
    var pot = makeCanvas(1024, 1024);
    var pctx = pot.getContext('2d');
    pctx.imageSmoothingEnabled = true;
    pctx.drawImage(tileCanvas, 0, 0, 1024, 1024);
    return { canvas: pot, aspect: finalH / finalW, widthFrac: finalW / w, seamless: seamless };
  }

  // ---------- derived mask data ----------

  function maskAlphaAt(w, h) {
    var small = downscale(maskCanvas, photoW, photoH, w, h);
    var d = small.getContext('2d').getImageData(0, 0, w, h).data;
    var m = new Float32Array(w * h);
    for (var i = 0; i < m.length; i++) m[i] = d[i * 4 + 3] / 255;
    return m;
  }

  // Low-frequency lighting map: luminance of the photo blurred ONLY over the selected area
  // (normalized convolution, so lawn/walls next to the area do not leak in), relative to the
  // brightly-lit part of the area. Carries shadows onto the new paving without imprinting the old
  // surface's texture (gravel, grass, cracks). Also adds a faint contact shadow along the edges.
  function rebuildShadeMap() {
    var s = SHADE_LONG_SIDE / Math.max(photoW, photoH);
    var w = Math.max(8, Math.round(photoW * s)), h = Math.max(8, Math.round(photoH * s));
    var photoSmall = downscale(photoImg, photoW, photoH, w, h);
    var pd = photoSmall.getContext('2d').getImageData(0, 0, w, h).data;
    var m = maskAlphaAt(w, h);
    var lm = new Float32Array(w * h), i;
    for (i = 0; i < m.length; i++) {
      var l = (0.299 * pd[i * 4] + 0.587 * pd[i * 4 + 1] + 0.114 * pd[i * 4 + 2]) / 255;
      lm[i] = Math.pow(l, 2.2) * m[i];
    }
    var r = Math.max(2, Math.round(w * 0.008));
    var num = boxBlur(lm, w, h, r, 3), den = boxBlur(m, w, h, r, 3);
    var wide = boxBlur(m, w, h, Math.max(3, Math.round(w * 0.02)), 2);

    var vals = [], lumAt = new Float32Array(w * h);
    for (i = 0; i < m.length; i++) {
      lumAt[i] = den[i] > 0.02 ? num[i] / den[i] : 0;
      if (m[i] > 0.5) vals.push(lumAt[i]);
    }
    var ref = 0.25;
    if (vals.length > 0) {
      vals.sort(function (a, b) { return a - b; });
      ref = Math.max(0.01, vals[Math.min(vals.length - 1, Math.floor(vals.length * 0.75))]);
    }
    shadeCanvas = shadeCanvas && shadeCanvas.width === w && shadeCanvas.height === h ? shadeCanvas : makeCanvas(w, h);
    var sctx = shadeCanvas.getContext('2d');
    var out = sctx.createImageData(w, h), od = out.data;
    for (i = 0; i < m.length; i++) {
      var shade = den[i] > 0.02 ? Math.pow(lumAt[i] / ref, 0.9) : 1;
      shade = Math.min(1.35, Math.max(0.12, shade));
      shade *= 0.84 + 0.16 * smoothstep(0.3, 0.85, wide[i]); // contact shadow at the boundary
      var v = Math.round(Math.min(1, shade / SHADE_RANGE) * 255);
      od[i * 4] = v; od[i * 4 + 1] = v; od[i * 4 + 2] = v; od[i * 4 + 3] = 255;
    }
    sctx.putImageData(out, 0, 0);
    shadeInfo = { ref: ref, w: w, h: h };
  }

  function rebuildSoftMask() {
    // Cross-browser feather for the canvas-2D fallback (the WebGL path feathers in the shader).
    var small = downscale(maskCanvas, photoW, photoH, Math.max(1, photoW / 3), Math.max(1, photoH / 3));
    softMask = makeCanvas(photoW, photoH);
    var ctx = softMask.getContext('2d');
    ctx.imageSmoothingEnabled = true;
    ctx.drawImage(small, 0, 0, photoW, photoH);
  }

  function rebuildMaskDerived() {
    rebuildShadeMap();
    if (gl) {
      if (!contextLost) {
        maskTexture = uploadTexture(maskTexture, maskCanvas, false);
        shadeTexture = uploadTexture(shadeTexture, shadeCanvas, false);
      }
    } else {
      rebuildSoftMask();
      // Per-mask pixel buffers for the canvas-2D fallback, so a slider move only re-warps.
      var shadeFull = makeCanvas(photoW, photoH);
      var sfctx = shadeFull.getContext('2d');
      sfctx.imageSmoothingEnabled = true;
      sfctx.drawImage(shadeCanvas, 0, 0, photoW, photoH);
      fallbackPixels = {
        shade: sfctx.getImageData(0, 0, photoW, photoH).data,
        mask: softMask.getContext('2d').getImageData(0, 0, photoW, photoH).data
      };
    }
    drawMaskTint();
  }

  function drawMaskTint() {
    if (!editCanvas) return;
    var ctx = editCanvas.getContext('2d');
    ctx.clearRect(0, 0, photoW, photoH);
    if (!maskPresent || !maskVisible) return;
    ctx.drawImage(maskCanvas, 0, 0);
    ctx.globalCompositeOperation = 'source-in';
    ctx.fillStyle = 'rgba(40, 200, 90, 0.4)';
    ctx.fillRect(0, 0, photoW, photoH);
    ctx.globalCompositeOperation = 'source-over';
  }

  // Mask statistics at low resolution: bbox, and per-row left/right extents for the horizon fit.
  function maskProfile() {
    var w = 256, h = Math.max(1, Math.round(photoH / photoW * 256));
    var m = maskAlphaAt(w, h);
    var left = new Int16Array(h).fill(-1), right = new Int16Array(h).fill(-1), count = new Int32Array(h);
    var minX = w, minY = h, maxX = -1, maxY = -1;
    for (var y = 0; y < h; y++)
      for (var x = 0; x < w; x++)
        if (m[y * w + x] > 0.5) {
          count[y]++;
          if (left[y] < 0) left[y] = x;
          right[y] = x;
          if (x < minX) minX = x; if (x > maxX) maxX = x;
          if (y < minY) minY = y; if (y > maxY) maxY = y;
        }
    return { w: w, h: h, left: left, right: right, count: count,
             found: maxX >= 0, minX: minX, minY: minY, maxX: maxX, maxY: maxY };
  }

  function fitLine(ys, xs) {
    var n = ys.length, sy = 0, sx = 0, syy = 0, sxy = 0, i;
    for (i = 0; i < n; i++) { sy += ys[i]; sx += xs[i]; syy += ys[i] * ys[i]; sxy += ys[i] * xs[i]; }
    var den = n * syy - sy * sy;
    if (n < 2 || Math.abs(den) < 1e-9) return null;
    var b = (n * sxy - sy * sx) / den, a = (sx - b * sy) / n, err = 0;
    for (i = 0; i < n; i++) { var e = xs[i] - (a + b * ys[i]); err += e * e; }
    return { a: a, b: b, rms: Math.sqrt(err / n) };
  }

  // Horizon row estimate (low-res units). Paths/driveways: the mask's left and right borders are
  // straight lines converging at the vanishing point, which lies on the horizon. Otherwise: a prior
  // for hand-held ground photos (40% from the top), always kept above the selected area.
  function estimateHorizon(p) {
    var top = p.minY, bottom = p.maxY, span = bottom - top + 1;
    var marginTop = top - 0.04 * p.h;
    var fallback = Math.min(0.40 * p.h, marginTop);
    if (span < p.h * 0.12) return { y: fallback, source: 'prior' };
    var ly = [], lx = [], ry = [], rx = [];
    for (var y = Math.round(top + span * 0.12); y <= bottom; y++) {
      if (p.count[y] < 2) continue;
      if (p.left[y] > 1) { ly.push(y); lx.push(p.left[y]); }
      if (p.right[y] < p.w - 2) { ry.push(y); rx.push(p.right[y]); }
    }
    var need = span * 0.35;
    if (ly.length < need || ry.length < need) return { y: fallback, source: 'prior' };
    var L = fitLine(ly, lx), R = fitLine(ry, rx);
    if (!L || !R) return { y: fallback, source: 'prior' };
    var ok = L.b < -0.08 && R.b > 0.08 && L.rms < p.w * 0.025 && R.rms < p.w * 0.025;
    if (!ok) return { y: fallback, source: 'prior' };
    var vy = (R.a - L.a) / (L.b - R.b);
    if (!(vy < top + 0.02 * p.h) || vy < -0.6 * p.h) return { y: fallback, source: 'prior' };
    return { y: Math.min(vy, top - 0.005 * p.h), source: 'vanishing-point' };
  }

  // Pinhole camera at CAMERA_HEIGHT_M, level (no roll), pitched down so the horizon sits at row yh.
  function cameraModel(yh) {
    var f = FOCAL_PER_LONG_SIDE * Math.max(photoW, photoH);
    var cx = photoW / 2, cy = photoH / 2;
    var theta = Math.atan2(cy - yh, f), ct = Math.cos(theta), st = Math.sin(theta), hc = CAMERA_HEIGHT_M;
    return {
      project: function (X, Z) {
        var zc = hc * st + Z * ct;
        return [cx + f * X / zc, cy + f * (hc * ct - Z * st) / zc];
      },
      distanceAtRow: function (v) {
        var d = v - cy, den = d * ct + f * st;
        return den > 1e-6 ? hc * (f * ct - d * st) / den : Infinity;
      },
      lateralAt: function (u, Z) { return (u - cx) * (hc * st + Z * ct) / f; }
    };
  }

  // Automatic ground rectangle: near edge on the bottom of the selection, far edge near its top,
  // sides at its left/right extent; corners projected with the camera model (true perspective).
  function computeAutoPerspective() {
    var p = maskProfile();
    if (!p.found) {
      p = { w: 256, h: Math.round(photoH / photoW * 256), minX: 0, maxX: 255, minY: 0, maxY: 0, found: false };
      p.minY = Math.round(p.h * 0.45); p.maxY = p.h - 1; p.minX = Math.round(p.w * 0.1); p.maxX = Math.round(p.w * 0.9);
    }
    var k = photoW / p.w;
    var hz = p.found ? estimateHorizon(p) : { y: Math.min(0.40 * p.h, p.minY - 0.04 * p.h), source: 'prior' };
    var yh = hz.y * k;
    var cam = cameraModel(yh);
    // The rectangle only positions the 4 drag handles (the plane itself comes from the camera
    // model), so keep it a little inside the photo where the handles are easy to grab.
    var nearRow = Math.min(photoH * 0.95, (p.maxY + 1) * k);
    var farRow = Math.max(p.minY * k, yh + 0.06 * photoH);
    var z0 = cam.distanceAtRow(nearRow);
    var z1 = cam.distanceAtRow(Math.min(farRow, nearRow - 0.05 * photoH));
    if (!isFinite(z0) || z0 <= 0) z0 = 2;
    if (!isFinite(z1) || z1 <= z0 * 1.15) z1 = z0 * 1.15 + 0.5;
    z1 = Math.min(z1, z0 + 60);
    var x0 = cam.lateralAt(Math.max(photoW * 0.04, p.minX * k), z0);
    var x1 = cam.lateralAt(Math.min(photoW * 0.96, (p.maxX + 1) * k), z0);
    if (x1 - x0 < 0.3) { var mid = (x0 + x1) / 2; x0 = mid - 0.15; x1 = mid + 0.15; }
    var tl = cam.project(x0, z1), tr = cam.project(x1, z1), br = cam.project(x1, z0), bl = cam.project(x0, z0);
    return {
      corners: [tl[0], tl[1], tr[0], tr[1], br[0], br[1], bl[0], bl[1]],
      groundW: x1 - x0, groundD: z1 - z0,
      horizonY: yh, horizonSource: hz.source
    };
  }

  function applyCorners(c) {
    var srcGround = [0, 0, groundW, 0, groundW, groundD, 0, groundD];
    var h = computeHomography(srcGround, c);
    var inv = invert3(h);
    // Degenerate quads (3 corners on a line, 2 corners on one spot) have no homography:
    // keep the last valid one instead of feeding NaN to the renderer.
    if (!h.concat(inv).every(isFinite)) return false;
    corners = c.slice();
    groundToPx = h;
    pxToGround = inv;
    drawGrid();
    return true;
  }

  function drawGrid() {
    if (!gridCanvas) return;
    var ctx = gridCanvas.getContext('2d');
    ctx.clearRect(0, 0, gridCanvas.width, gridCanvas.height);
    if (!gridVisible || !groundToPx) return;
    var lw = Math.max(1.5, photoW / 700);
    ctx.lineWidth = lw;
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.75)';
    ctx.shadowColor = 'rgba(0, 0, 0, 0.6)';
    ctx.shadowBlur = lw * 2;
    var nx = 6, ny = 8, i, j, p;
    for (i = 0; i <= nx; i++) {
      ctx.beginPath();
      for (j = 0; j <= 24; j++) {
        p = applyH(groundToPx, groundW * i / nx, groundD * j / 24);
        if (j === 0) ctx.moveTo(p[0], p[1]); else ctx.lineTo(p[0], p[1]);
      }
      ctx.stroke();
    }
    for (j = 0; j <= ny; j++) {
      // rows spaced evenly in ground metres -> they visibly bunch up toward the far edge
      p = applyH(groundToPx, 0, groundD * j / ny);
      var q = applyH(groundToPx, groundW, groundD * j / ny);
      ctx.beginPath(); ctx.moveTo(p[0], p[1]); ctx.lineTo(q[0], q[1]); ctx.stroke();
    }
  }

  // ---------- lifecycle ----------

  // Reset all non-GL module state so a fresh init() starts from a clean slate.
  function resetState() {
    photoW = 0; photoH = 0;
    maskCanvas = null; softMask = null; maskPresent = false; maskVisible = true;
    shadeCanvas = null; shadeInfo = null;
    corners = null; groundToPx = null; pxToGround = null; groundW = 10; groundD = 15;
    gridVisible = false;
    tileSource = null; tileAspect = 1; tileMeters = 1.0; scaleFactor = 1.0; rotationRad = 0;
    currentTextureKey = null; currentTile = null; textureRequest++;
    contextLost = false; fallbackBusy = false; fallbackDirty = false; fallbackPixels = null;
    samCanvas = null; addLayer = null; eraseLayer = null;
    // Interaction layer: a fresh init() must not carry over an active
    // drawing mode or a mid-stroke flag from a previous session's mask editing.
    mode = null; stroking = false; strokeMoved = false; lastPaint = null;
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
    maskTexture = null; tileTexture = null; shadeTexture = null;
  }

  // Mobile browsers drop WebGL contexts (e.g. tab backgrounded on iOS). Ask for a restore and
  // rebuild the program + all textures from the CPU-side canvases we keep anyway.
  var contextLost = false;
  function onContextLost(e) {
    if (e.target !== glCanvas) return; // late event from a disposed session's canvas
    e.preventDefault();
    contextLost = true;
  }
  function onContextRestored(e) {
    if (e.target !== glCanvas) return;
    contextLost = false;
    maskTexture = null; tileTexture = null; shadeTexture = null;
    try {
      if (!initGl()) return;
      if (photoW) gl.viewport(0, 0, photoW, photoH);
      if (maskCanvas && shadeCanvas) {
        maskTexture = uploadTexture(null, maskCanvas, false);
        shadeTexture = uploadTexture(null, shadeCanvas, false);
      }
      if (tileSource) tileTexture = uploadTexture(null, tileSource, true);
      api.render();
    } catch (err) { console.warn('WebGL restore failed:', err); }
  }

  function loadImage(url) {
    return new Promise(function (resolve, reject) {
      var img = new Image();
      img.crossOrigin = 'anonymous';
      img.onload = function () { resolve(img); };
      img.onerror = reject;
      img.src = url;
    });
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
      photoImg.alt = '';
      glCanvas = document.createElement('canvas');
      glCanvas.style.cssText = 'position:absolute;inset:0;width:100%;height:100%;pointer-events:none;';
      gridCanvas = document.createElement('canvas');
      gridCanvas.style.cssText = 'position:absolute;inset:0;width:100%;height:100%;pointer-events:none;';
      editCanvas = document.createElement('canvas');
      editCanvas.style.cssText = 'position:absolute;inset:0;width:100%;height:100%;touch-action:none;';
      divider = document.createElement('div');
      divider.style.cssText = 'position:absolute;top:0;bottom:0;width:0;border-left:2px solid #fff;' +
        'box-shadow:0 0 6px rgba(0,0,0,.6);pointer-events:none;display:none;';
      divider.innerHTML =
        '<span style="position:absolute;top:8px;right:8px;background:rgba(0,0,0,.6);color:#fff;font-size:12px;padding:2px 6px;border-radius:3px;white-space:nowrap">Преди</span>' +
        '<span style="position:absolute;top:8px;left:8px;background:rgba(0,0,0,.6);color:#fff;font-size:12px;padding:2px 6px;border-radius:3px;white-space:nowrap">След</span>';
      stage.appendChild(photoImg);
      stage.appendChild(glCanvas);
      stage.appendChild(gridCanvas);
      stage.appendChild(editCanvas);
      stage.appendChild(divider);

      var webgl = false;
      if (!forceFallback) {
        try { webgl = initGl(); } catch (e) { console.warn('WebGL unavailable:', e); webgl = false; }
      }
      if (!webgl && gl) {
        // A canvas that already holds a WebGL context can never return a 2D context, so the
        // fallback renderer needs a fresh canvas element; also release the half-built context.
        releaseGl();
        var fresh = glCanvas.cloneNode(false);
        stage.replaceChild(fresh, glCanvas);
        glCanvas = fresh;
      }
      if (!webgl) gl = null;
      if (gl) {
        glCanvas.addEventListener('webglcontextlost', onContextLost);
        glCanvas.addEventListener('webglcontextrestored', onContextRestored);
      }
      if (api._wireEvents) api._wireEvents();
      return { webgl: !!gl };
    },

    loadPhotoFromDataUrl: function (dataUrl) {
      return new Promise(function (resolve, reject) {
        photoImg.onload = function () {
          photoW = photoImg.naturalWidth; photoH = photoImg.naturalHeight;
          glCanvas.width = photoW; glCanvas.height = photoH;
          editCanvas.width = photoW; editCanvas.height = photoH;
          gridCanvas.width = photoW; gridCanvas.height = photoH;
          maskCanvas = makeCanvas(photoW, photoH);   // transparent = nothing selected
          samCanvas = makeCanvas(photoW, photoH);
          addLayer = makeCanvas(photoW, photoH);
          eraseLayer = makeCanvas(photoW, photoH);
          maskPresent = false;
          if (gl) {
            gl.viewport(0, 0, photoW, photoH);
            gl.clear(gl.COLOR_BUFFER_BIT);
          }
          resolve({ width: photoW, height: photoH });
        };
        photoImg.onerror = reject;
        photoImg.src = dataUrl;
      });
    },

    // Server mask: 8-bit grayscale PNG (white = selected). Converted to the alpha convention.
    setMaskPng: function (base64) {
      return new Promise(function (resolve, reject) {
        var img = new Image();
        img.onload = function () {
          if (!samCanvas) { resolve(); return; } // disposed while the mask was decoding
          var ctx = samCanvas.getContext('2d');
          ctx.clearRect(0, 0, photoW, photoH);
          ctx.drawImage(img, 0, 0, photoW, photoH);
          var id = ctx.getImageData(0, 0, photoW, photoH), d = id.data;
          for (var i = 0; i < d.length; i += 4) {
            d[i + 3] = d[i]; d[i] = 255; d[i + 1] = 255; d[i + 2] = 255;
          }
          ctx.putImageData(id, 0, 0);
          composeMask();
          maskPresent = true;
          rebuildMaskDerived();
          resolve();
        };
        img.onerror = reject;
        img.src = 'data:image/png;base64,' + base64;
      });
    },

    clearMask: function () {
      [maskCanvas, samCanvas, addLayer, eraseLayer].forEach(function (c) {
        c.getContext('2d').clearRect(0, 0, photoW, photoH);
      });
      maskPresent = false;
      rebuildMaskDerived();
      if (gl) gl.clear(gl.COLOR_BUFFER_BIT);
      else glCanvas.getContext('2d').clearRect(0, 0, photoW, photoH);
    },

    hasMask: function () { return maskPresent; },

    // Green highlight of the selected area (used while editing the selection).
    setMaskVisible: function (visible) {
      maskVisible = !!visible;
      drawMaskTint();
    },

    // Computes the automatic perspective from the current mask and APPLIES it. Returns corners.
    autoPerspective: function () {
      var auto = computeAutoPerspective();
      var prevW = groundW, prevD = groundD;
      groundW = auto.groundW; groundD = auto.groundD;
      if (!applyCorners(auto.corners)) { groundW = prevW; groundD = prevD; }
      return corners ? corners.slice() : auto.corners;
    },

    getCorners: function () { return corners ? corners.slice() : null; },

    // Move the 4 corners of the ground rectangle (keeps its real-world size).
    setCorners: function (c) { applyCorners(c); },

    setGridVisible: function (visible) { gridVisible = !!visible; drawGrid(); },

    // Product switches can overlap (a slow uncached texture, then a cached one): only the most
    // recent request may change what is shown, and its width travels with its texture.
    // prepared: true for a dedicated, seamless visualizer texture (used as-is), false for a
    // catalog photo standing in for one (automatically flattened, made tileable, randomized).
    setProductTexture: function (url, widthMeters, prepared) {
      var request = ++textureRequest;
      var meters = widthMeters || 1.0;
      var key = url + '|' + (prepared ? 'prepared' : 'photo');
      if (currentTextureKey === key && tileSource) { tileMeters = meters; return Promise.resolve(); }
      for (var i = 0; i < textureCache.length; i++) {
        if (textureCache[i].key === key) {
          var hit = textureCache.splice(i, 1)[0];
          textureCache.push(hit);
          useTile(hit, meters);
          return Promise.resolve();
        }
      }
      return loadImage(url).then(function (img) {
        var entry = null;
        for (var j = 0; j < textureCache.length; j++) if (textureCache[j].key === key) entry = textureCache[j];
        if (!entry) {
          var tile = prepareTile(img, prepared);
          entry = { key: key, canvas: tile.canvas, aspect: tile.aspect, widthFrac: tile.widthFrac,
                    seamless: tile.seamless };
          textureCache.push(entry);
          if (textureCache.length > TEXTURE_CACHE_MAX) textureCache.shift();
        }
        if (request === textureRequest) useTile(entry, meters); // a newer selection wins
      });
    },

    setScale: function (f) { scaleFactor = f; },
    setRotation: function (deg) { rotationRad = deg * Math.PI / 180; },

    render: function () {
      if (!maskPresent || !tileSource || !pxToGround) return;
      if (!gl) { scheduleFallbackRender(); return; }
      if (contextLost) return; // re-rendered on 'webglcontextrestored'
      gl.clear(gl.COLOR_BUFFER_BIT);
      gl.uniform2f(uniforms.u_size, photoW, photoH);
      var m = pxToGround; // row-major -> column-major for uniformMatrix3fv
      gl.uniformMatrix3fv(uniforms.u_invH, false,
        [m[0], m[3], m[6], m[1], m[4], m[7], m[2], m[5], m[8]]);
      var tw = tileWidthMeters();
      gl.uniform2f(uniforms.u_tileSize, tw, tw * tileAspect);
      gl.uniform1f(uniforms.u_rot, rotationRad);
      gl.uniform1f(uniforms.u_shadeRange, SHADE_RANGE);
      gl.uniform1f(uniforms.u_stochastic, currentTile && currentTile.seamless ? 0 : 1);
      gl.activeTexture(gl.TEXTURE0); gl.bindTexture(gl.TEXTURE_2D, tileTexture);
      gl.activeTexture(gl.TEXTURE1); gl.bindTexture(gl.TEXTURE_2D, maskTexture);
      gl.activeTexture(gl.TEXTURE2); gl.bindTexture(gl.TEXTURE_2D, shadeTexture);
      gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
    },

    // percent = width (from the left) that shows the original photo.
    setCompareRatio: function (percent) {
      var p = Math.min(100, Math.max(0, percent));
      // Only the rendered layer is clipped: CSS clip-path also clips hit-testing, and the edit
      // canvas must keep receiving taps/brush strokes across the whole photo.
      glCanvas.style.clipPath = 'inset(0 0 0 ' + p + '%)';
      if (p > 0 && p < 100) {
        divider.style.display = 'block';
        divider.style.left = p + '%';
      } else {
        divider.style.display = 'none';
      }
    },

    getStageRect: function () {
      var r = photoImg.getBoundingClientRect();
      return { left: r.left, top: r.top, width: r.width, height: r.height };
    },

    exportResultDataUrl: function () {
      var out = makeCanvas(photoW, photoH);
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

    _test: {
      computeHomography: computeHomography, applyH: applyH, invert3: invert3,
      quiltWrapX: quiltWrapX, makeTileable: makeTileable, estimateHorizon: estimateHorizon, fitLine: fitLine
    },
    _internal: function () {
      return {
        get maskCanvas() { return maskCanvas; },
        get photoImg() { return photoImg; },
        get glCanvas() { return glCanvas; },
        get editCanvas() { return editCanvas; },
        get groundToPx() { return groundToPx; },
        get ground() { return { w: groundW, d: groundD }; },
        get dotNetRef() { return dotNetRef; },
        get size() { return { w: photoW, h: photoH }; },
        get tile() { return { source: tileSource, meters: tileMeters, aspect: tileAspect, scale: scaleFactor, rot: rotationRad }; },
        get shadeCanvas() { return shadeCanvas; },
        get shadeInfo() { return shadeInfo; },
        get softMask() { return softMask; },
        autoPerspectiveInfo: computeAutoPerspective,
        setMaskPresent: function (v) { maskPresent = v; },
        rebuildMaskDerived: rebuildMaskDerived
      };
    }
  };

  function tileWidthMeters() {
    return Math.max(0.01, tileMeters * (currentTile ? currentTile.widthFrac : 1) * scaleFactor);
  }

  var currentTile = null, textureRequest = 0;
  function useTile(entry, meters) {
    currentTile = entry;
    currentTextureKey = entry.key;
    tileSource = entry.canvas;
    tileAspect = entry.aspect;
    tileMeters = meters;
    if (gl && !contextLost) tileTexture = uploadTexture(tileTexture, entry.canvas, true);
  }

  // ---------- interaction layer ----------

  var mode = null, brushSize = 40, stroking = false, strokeMoved = false, lastPaint = null;

  function eventToPhotoPx(evt) {
    var rect = photoImg.getBoundingClientRect();
    return {
      x: (evt.clientX - rect.left) / rect.width * photoW,
      y: (evt.clientY - rect.top) / rect.height * photoH
    };
  }

  // Brush size is given in screen pixels so it feels the same at any zoom/photo size.
  function brushPx() {
    var rect = photoImg.getBoundingClientRect();
    var k = rect.width > 0 ? photoW / rect.width : 1;
    return brushSize * k;
  }

  function composeMask() {
    var ctx = maskCanvas.getContext('2d');
    ctx.clearRect(0, 0, photoW, photoH);
    ctx.drawImage(samCanvas, 0, 0);
    ctx.drawImage(addLayer, 0, 0);
    ctx.globalCompositeOperation = 'destination-out';
    ctx.drawImage(eraseLayer, 0, 0);
    ctx.globalCompositeOperation = 'source-over';
  }

  function strokeOn(canvas, op, x, y, size) {
    var ctx = canvas.getContext('2d');
    ctx.globalCompositeOperation = op;
    ctx.fillStyle = '#fff';
    ctx.strokeStyle = '#fff';
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    ctx.lineWidth = size;
    ctx.beginPath();
    if (lastPaint) { // continuous stroke: no gaps on fast pointer moves
      ctx.moveTo(lastPaint.x, lastPaint.y);
      ctx.lineTo(x, y);
      ctx.stroke();
    } else {
      ctx.arc(x, y, size / 2, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.globalCompositeOperation = 'source-over';
  }

  function paintAt(x, y) {
    var size = brushPx(), erase = mode === 'erase';
    // Visible mask + the persistent manual layers (brush adds / eraser removes, each undoing the other).
    strokeOn(maskCanvas, erase ? 'destination-out' : 'source-over', x, y, size);
    strokeOn(erase ? eraseLayer : addLayer, 'source-over', x, y, size);
    strokeOn(erase ? addLayer : eraseLayer, 'destination-out', x, y, size);
    lastPaint = { x: x, y: y };
    maskPresent = true;
    drawMaskTint();
  }

  // Commit an in-progress brush/erase stroke: rebuild derived mask data,
  // re-render, and notify .NET. Safe to call when no stroke is active.
  function finalizeStroke() {
    if (!stroking) return;
    stroking = false;
    lastPaint = null;
    maskPresent = hasAnySelection();
    rebuildMaskDerived();
    if (maskPresent) api.render();
    else if (gl) gl.clear(gl.COLOR_BUFFER_BIT);
    if (dotNetRef) dotNetRef.invokeMethodAsync('OnMaskEditedAsync', maskPresent);
  }

  function hasAnySelection() {
    var p = maskAlphaAt(64, Math.max(1, Math.round(photoH / photoW * 64)));
    for (var i = 0; i < p.length; i++) if (p[i] > 0.02) return true;
    return false;
  }

  function onPointerDown(evt) {
    if (!mode || !photoW) return;
    evt.preventDefault();
    if (mode === 'brush' || mode === 'erase') {
      stroking = true;
      lastPaint = null;
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
    editCanvas.addEventListener('pointercancel', function () { finalizeStroke(); });
  };
  api._test.strokeForTest = function (x, y) { // deterministic brush for the harness
    lastPaint = null;
    paintAt(x, y);
    lastPaint = null;
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

  // Leading-edge render + at most one trailing render per animation frame: slider and handle
  // input can fire far faster than the CPU warp can run.
  var fallbackBusy = false, fallbackDirty = false, fallbackPixels = null;
  function scheduleFallbackRender() {
    if (fallbackBusy) { fallbackDirty = true; return; }
    api._renderFallback();
    fallbackBusy = true;
    requestAnimationFrame(function () {
      fallbackBusy = false;
      if (fallbackDirty) { fallbackDirty = false; if (maskPresent && tileSource && pxToGround) scheduleFallbackRender(); }
    });
  }

  api._renderFallback = function () {
    var ctx = glCanvas.getContext('2d');
    if (!ctx || !fallbackPixels) return;
    ctx.clearRect(0, 0, photoW, photoH);

    // Ground area to cover: the corner rectangle extended sideways and nearer/farther, limited to
    // points in front of the camera (w > 0), so the whole selection gets paved, not only the quad.
    var ex0 = -groundW * 2, ex1 = groundW * 3, ey0 = -groundD * 2, ey1 = groundD * 1.5;
    var spanX = ex1 - ex0, spanY = ey1 - ey0;
    var tw = tileWidthMeters(), th = tw * tileAspect;
    var ppm = Math.min(256 / tw, 2048 / Math.max(spanX, spanY));
    var big = makeCanvas(spanX * ppm, spanY * ppm);
    var bctx = big.getContext('2d');
    var tileScaled = makeCanvas(Math.max(4, tw * ppm), Math.max(4, th * ppm));
    tileScaled.getContext('2d').drawImage(tileSource, 0, 0, tileScaled.width, tileScaled.height);
    bctx.save();
    bctx.translate(-ex0 * ppm, -ey0 * ppm);  // ground origin
    bctx.rotate(rotationRad);                // same direction as the shader's mat2 rotation
    bctx.fillStyle = bctx.createPattern(tileScaled, 'repeat');
    var diag = Math.hypot(big.width, big.height) * 2;
    bctx.fillRect(-diag, -diag, diag * 2, diag * 2);
    bctx.restore();

    var paved = makeCanvas(photoW, photoH);
    var pctx = paved.getContext('2d');
    var cells = 24;
    function wOf(x, y) { return groundToPx[6] * x + groundToPx[7] * y + groundToPx[8]; }
    for (var gy = 0; gy < cells; gy++) {
      for (var gx = 0; gx < cells; gx++) {
        var gx0 = ex0 + gx / cells * spanX, gx1 = ex0 + (gx + 1) / cells * spanX;
        var gy0 = ey0 + gy / cells * spanY, gy1 = ey0 + (gy + 1) / cells * spanY;
        if (wOf(gx0, gy0) <= 0.02 || wOf(gx1, gy0) <= 0.02 || wOf(gx1, gy1) <= 0.02 || wOf(gx0, gy1) <= 0.02)
          continue; // cell reaches the horizon (or behind the camera): projection degenerates
        var p00 = applyH(groundToPx, gx0, gy0), p10 = applyH(groundToPx, gx1, gy0);
        var p11 = applyH(groundToPx, gx1, gy1), p01 = applyH(groundToPx, gx0, gy1);
        var sx0 = (gx0 - ex0) * ppm, sx1 = (gx1 - ex0) * ppm, sy0 = (gy0 - ey0) * ppm, sy1 = (gy1 - ey0) * ppm;
        drawTriangle(pctx, big, [sx0, sy0, sx1, sy0, sx1, sy1],
          [p00[0], p00[1], p10[0], p10[1], p11[0], p11[1]]);
        drawTriangle(pctx, big, [sx0, sy0, sx1, sy1, sx0, sy1],
          [p00[0], p00[1], p11[0], p11[1], p01[0], p01[1]]);
      }
    }

    // Lighting + mask clip per pixel (same maths as the shader, gamma ~2 approximation).
    var sd = fallbackPixels.shade, md = fallbackPixels.mask;
    var img = pctx.getImageData(0, 0, photoW, photoH), pd = img.data;
    for (var i = 0; i < pd.length; i += 4) {
      var a = pd[i + 3] / 255 * md[i + 3] / 255;
      if (a <= 0) { pd[i + 3] = 0; continue; }
      var sh = Math.sqrt(sd[i] / 255 * SHADE_RANGE);
      pd[i] = Math.min(255, pd[i] * sh);
      pd[i + 1] = Math.min(255, pd[i + 1] * sh);
      pd[i + 2] = Math.min(255, pd[i + 2] * sh);
      pd[i + 3] = Math.round(a * 255);
    }
    pctx.putImageData(img, 0, 0);
    ctx.drawImage(paved, 0, 0);
  };

  return api;
})();
