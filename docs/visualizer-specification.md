# Functional & Technical Specification — Product Visualizer (Визуализатор)

**Status**: Draft v2 — approach fixed to **Option 2: AI segmentation + classical texture rendering** (owner decision, 05.07.2026). Supersedes v1, which recommended full generative AI editing. Option 4 (full generative edit) is preserved as a later comparison track — see `visualizer-specification-option4.md` and `docs/plans/visualizer-option4-implementation-plan.md`.
**Depends on**: `technical-specification.md`, `database-schema.md`, `api-endpoints.md`, `conventions.md`
**Implementation plan**: `docs/superpowers/plans/2026-07-05-product-visualizer.md`

---

## 1. Feature Overview

A customer uploads a photo of their property (driveway, yard, garden path, terrace), the system finds the ground area to be paved, and renders the customer's photo with that area re-paved using the **actual texture** of a selected paving stone product. A product panel lets the customer switch products; the visualization updates **instantly** because rendering is deterministic and local.

Industry name: **product / surface / paver visualizer** — an established e-commerce category (Roomvo, Chameleon Power, Renoworks sell it as enterprise white-label SaaS; vendor-reported conversion uplifts of 5–10×). Under the hood, the classical commercial pipeline is exactly this approach: segment the surface, warp the real product texture into it, transfer the scene lighting.

### 1.1 The chosen approach in one paragraph

AI is used for **one job only: finding the area** (segmentation). Everything the customer actually sees is rendered deterministically from the product's real texture image — no generative AI invents pixels. Consequences:

| Property | Result |
|----------|--------|
| Texture fidelity | **Exact** — the customer sees the real product texture, color, and pattern |
| Per-use cost | **~0 €** — segmentation runs on our own server CPU; rendering runs in the customer's browser |
| Product switching | **Instant** (<0.5 s) — re-render locally, no server round-trip |
| Privacy | Photo goes only to **our** server, is deleted minutes later, and never reaches any third party |
| Realism ceiling | Lower than generative AI — perspective is approximated, lighting is transferred not simulated. Accepted trade-off, mitigated in §5.3 |

Rejected alternatives (analyzed in v1 and in §5.5): enterprise SaaS (wrong size), full generative AI editing (approximated textures, per-image fees, third-party photo transfer), fully manual marking (too much customer effort — kept as built-in fallback).

---

## 2. Goals & Non-Goals

### Goals
1. Customer sees their own terrain re-paved with the **exact texture** of a selected product, with believable perspective and preserved scene lighting/shadows.
2. AI proposes the paved area from a single tap; the customer can refine it with simple tools.
3. Product switching updates the visualization instantly.
4. Zero marginal cost per visualization; no third-party data processors.
5. Fully usable on mobile; 100% Bulgarian UI.

### Non-Goals (V1)
- Photorealistic shadow *casting* or 3D relief of the stones (only lighting *transfer* from the original photo).
- Area/quantity (м²) estimation from the photo.
- AR / live camera view; customer accounts; saving sessions server-side.
- Automatic perspective detection (V1.1 candidate via depth estimation, §13).

---

## 3. Functional Specification

### 3.1 Entry points

| Location | Element |
|----------|---------|
| Main navigation (public layout) | Link «Визуализатор» |
| Product detail page (visualizer-enabled products) | Button «Виж как ще изглежда при вас» — opens visualizer with product preselected |
| Home page | Promo section with before/after example and CTA «Опитай визуализатора» |

### 3.2 Page: Визуализатор (`/visualizer`) — customer flow

**Step 1 — Качване на снимка**
- Drag & drop (desktop) / camera capture (mobile: `accept="image/*" capture="environment"`).
- Guidance: „Снимайте площта така, че да се вижда цялата повърхност, която искате да покриете."
- Consent checkbox (required): „Съгласен/на съм снимката да бъде обработена на сървъра на магазина за целите на визуализацията. Снимката се изтрива автоматично след обработката." (Note: simpler than v1 — no third-party AI processor is involved.)
- Client-side: JPG/PNG/WebP, ≤10 MB; downscaled to ≤2048 px long side before upload.

**Step 2 — Маркиране на областта (AI segmentation)**
- Instruction: „Докоснете областта, която искате да покриете с настилка."
- Customer taps once on the target surface → the server's segmentation model returns a proposed mask → shown as a semi-transparent green overlay.
- Refinement tools (toolbar):
  - «Добави област» — additional positive tap (extends the mask to another region, e.g., a second path segment);
  - «Премахни» — negative tap (excludes a region, e.g., a flower bed inside the driveway);
  - «Четка» / «Гума» — manual brush paint/erase for fine corrections (also the full fallback if segmentation misfires);
  - «Изчисти» — start marking over.
- The mask automatically excludes objects standing on the surface (car, pots) when segmentation detects them; brush handles the rest.

**Step 3 — Перспектива и мащаб**
- A perspective grid (receding tile pattern) is overlaid, auto-initialized from the mask shape (wide at bottom = near, narrower at top = far).
- Four large draggable corner handles let the customer align the grid with the ground plane („Наместете мрежата по земята"). Defaults are chosen so that many users can skip this step entirely.
- Slider «Размер на камъка» adjusts texture scale (initialized from the product's real-world texture dimensions, §7.1); optional slider «Завъртане» for pattern direction.

**Step 4 — Продукт и резултат**
- Side panel (right on desktop / bottom sheet on mobile): visualizer-enabled products with thumbnail, name, price with ДДС per м²; category filter + name search; preselected product applied immediately.
- Switching products re-renders **instantly** — mask, perspective, and scale are kept.
- Result view: **before/after slider** (draggable divider; toggle «Преди / След» acceptable fallback).
- Steps 2–3 remain editable — changing the mask or grid re-renders live.

**Step 5 — Действия**
- «Изтегли изображението» — client-side download of the rendered canvas.
- «Добави в количката» — adds selected product via existing `CartService`.
- «Виж продукта» — link to product detail.
- «Нова снимка» — reset to Step 1.

### 3.3 Labels on the result
- Disclaimer (always visible under the result): „Визуализацията е ориентировъчна. Реалният продукт може да се различава по цвят и вид, а размерите са приблизителни."
- No generative AI creates image content in this design, so EU AI Act Art. 50 synthetic-content marking is likely **not** triggered (see §10.4); the honest disclaimer is kept regardless.

### 3.4 States & errors (Bulgarian)

| Situation | Behavior |
|-----------|----------|
| Segmentation service error/timeout | „Областта не можа да бъде разпозната автоматично. Можете да я маркирате ръчно с четката." → brush mode enabled (feature degrades, never blocks) |
| Photo rejected (format/size) | „Моля, качете снимка във формат JPG или PNG до 10 MB." |
| Rate limit (per IP) / global cap | „Достигнахте дневния лимит за визуализации. Опитайте отново утре." / „Визуализаторът е временно недостъпен." |
| Tap hits no recognizable surface | Small mask or empty → „Не разпознахме повърхност тук. Опитайте друго място или използвайте четката." |
| WebGL unavailable (old browser) | Canvas-2D fallback renderer (§5.3); if even that fails: „Браузърът ви не поддържа визуализатора." |
| Feature flag off / no enabled products | Page hidden from navigation |

---

## 4. System Architecture

```
┌────────────────────────────────┐          ┌─────────────────────────────────┐
│ Blazor WASM Client             │          │ NaturalStoneImpex.Api           │
│                                │          │                                 │
│ /visualizer page               │  HTTPS   │ VisualizerController            │
│  - photo downscale             ├─────────►│  POST /segment  (photo, taps)   │
│  - tap/brush mask editing      │◄─────────┤  → mask PNG (photo deleted      │
│  - perspective grid UI         │   mask   │     immediately after response) │
│  - WebGL texture renderer      │          │                                 │
│  - luminance (lighting) blend  │          │ SegmentationService             │
│  - before/after, download      │          │  - ONNX Runtime (CPU)           │
│                                │          │  - MobileSAM encoder+decoder    │
│ ALL RENDERING LOCAL            │          │  - mask post-processing         │
│ (product switch = no network)  │          │ Rate limiting / quotas          │
└────────────────────────────────┘          └─────────────────────────────────┘
```

**Division of labor** — the server does the one thing that needs a model (segmentation); the browser does everything visual. This is what makes product switching instant and keeps photos off third-party services. The photo is uploaded once for segmentation and deleted from the server as soon as the mask is returned; the browser keeps photo + mask in memory for the session.

Refinement taps (add/remove region) each require a segmentation call. To avoid re-uploading the photo per tap, the server caches the **image embedding** (SAM's encoder output, not the photo) in memory for ~15 minutes keyed by a session token — subsequent taps run only the lightweight decoder (<100 ms) against the cached embedding.

---

## 5. Detailed Design

### 5.1 Segmentation (server)

**Model**: a promptable segmentation model of the SAM family (Segment Anything), small variant — primary candidate **MobileSAM** (~10M-parameter encoder), fallback candidate SAM2-tiny. Both are **Apache-2.0 licensed** (safe for commercial use). Exported to ONNX; runs on **CPU** via `Microsoft.ML.OnnxRuntime` — no Python, no GPU, no external service.

**Why promptable (tap-based) instead of fully automatic semantic segmentation:**
1. *Robustness*: a driveway may be dirt, gravel, old concrete, grass, or a mix — a class-based model (road/path/grass) guesses which one the customer means; a tap tells us exactly which surface they want, in one gesture.
2. *Disambiguation*: photos often contain several ground regions (lawn + path + street) — only the customer knows which to pave.
3. *Licensing*: the natural semantic models for this task (SegFormer/ADE20K) carry a **non-commercial NVIDIA license**, and ADE20K-trained weights are a licensing gray area. SAM-family weights are cleanly Apache-2.0. (Verify the exact export's license at implementation time.)

**Pipeline** (per photo):
1. Preprocess: resize/letterbox to the model's input (1024×1024), normalize.
2. Encoder → image embedding (~1–4 s on CPU; run once, cached in `IMemoryCache` for 15 min under a random `sessionToken`).
3. Decoder with the tap coordinates (positive/negative points) → low-res mask logits (<100 ms).
4. Post-process: threshold; keep components connected to positive taps; morphological close/open (hand-rolled on the binary mask — no OpenCV dependency); upscale to original resolution; encode as 1-bit PNG.
5. Delete the uploaded photo file (or process fully in memory and never write it to disk — preferred).

**Concurrency**: encoder inference is CPU-heavy → `SemaphoreSlim` limiting concurrent encodes (default 2) + request queue timeout; per-IP daily quota (default 20 photos) and global daily cap (default 500) as denial-of-wallet/CPU protection.

### 5.2 Perspective model (client)

The paved ground is approximated as a **plane**; the mapping from texture space to image space is a **homography** computed from the 4 corner handles of the perspective grid (standard 4-point DLT — a ~40-line JS function; no library dependency required, though `glMatrix` may be used).

- **Initialization**: the grid quadrilateral is derived from the mask's bounding geometry — bottom edge at the mask's bottom (near), top edge at the mask's top narrowed to ~45% width (far), a heuristic that matches typical photos where the camera looks slightly down at the ground. Good defaults make Step 3 skippable for most photos.
- **Texture scale**: the homography maps a virtual ground rectangle of configurable physical size; the product's `TextureWidthMeters` (§7.1) determines how many texture tiles fit, so «Размер на камъка» starts at a physically plausible value.
- **Limitations accepted**: curved/sloped/stepped terrain deviates from the plane assumption — the visualization remains a believable approximation, per the disclaimer.

### 5.3 Rendering (client, per product — the instant part)

Renderer: **WebGL** (true projective texture mapping, fast on mobile), with a **canvas-2D fallback** (subdivision warp, perspective.js-style) for browsers without WebGL. Implemented as a plain-JS module (`visualizer.js`) driven from Blazor via JS interop; Blazor owns UI state, JS owns the canvas.

Compositing pipeline per render:
1. Tile the product's seamless texture (repeat wrap, scale/rotation from sliders).
2. Warp by the homography onto the photo plane (one textured quad in WebGL).
3. Clip to the mask (mask as alpha texture; feathered ~2 px edge to avoid hard cut-outs).
4. **Lighting transfer**: multiply the warped texture by the original photo's normalized luminance within the mask (`result = texture × L/L̄`, clamped). This carries real shadows (tree, building, car shadows) onto the new paving — the single highest-value realism trick in classical visualizers.
5. Draw over the original photo → final canvas.

Product switch changes only the texture in step 1 → full re-render in tens of milliseconds.

### 5.4 Graceful degradation ladder

1. Tap segmentation works → best case.
2. Segmentation poor → customer fixes with brush/eraser (same mask data structure).
3. Segmentation service down → pure manual brush marking (feature still works; this is v1's "Option 1" embedded as a fallback).
4. No WebGL → canvas-2D warp (slower, slightly lower quality).

### 5.5 Alternatives considered inside Option 2

| Alternative | Verdict |
|-------------|---------|
| Fully automatic semantic segmentation (SegFormer/DeepLab on ADE20K), zero taps | Rejected for V1: license risk (non-commercial weights), brittle on mixed surfaces, ambiguous with multiple ground regions. Could be added later as a "pre-tap" proposal on top of SAM |
| In-browser segmentation (onnxruntime-web) | Rejected for V1: smallest viable SAM builds are tens of MB of weights to download — hostile to mobile visitors. Revisit if a <10 MB quantized decoder-only split is adopted (§13) |
| Server-side rendering (ImageSharp/SkiaSharp composite) | Rejected: breaks instant product switching, adds server load, keeps photos on the server longer — client rendering is better on every axis for this feature |
| Depth-model auto-perspective (Depth Anything small, Apache-2.0) | Deferred to V1.1: promising way to remove Step 3 entirely, but adds a second model + plane-fitting math; manual handles are needed as a correction mechanism anyway |

---

## 6. API Design

New `VisualizerController` (thin) + `ISegmentationService`/`SegmentationService` in `Services/`.

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | `/api/visualizer/products` | Visualizer-enabled products (id, name, thumbnail, textureUrl, textureWidthMeters, price with ДДС, unit) | No |
| POST | `/api/visualizer/segment` | Multipart `photo` + JSON `points` (first call) → runs encoder+decoder | No |
| POST | `/api/visualizer/segment/{sessionToken}` | JSON `points` only — decoder against cached embedding (refinement taps) | No |

**Response (200)**:
```json
{ "sessionToken": "guid", "maskPng": "<base64>", "width": 2048, "height": 1536 }
```

Errors: standard `{ "error": "Българско съобщение" }`; 400 validation, 404 expired sessionToken (client silently re-uploads), 429 quota, 503 segmentation unavailable (client switches to manual brush mode).

**Upload security** (per Microsoft guidance): validate magic bytes not just extension; enforce server-side size limit; never use client file names; process in memory — no persisted upload files; strip nothing because nothing is stored.

**Configuration** (`appsettings.json`):
```json
"Visualizer": {
  "Enabled": true,
  "ModelPath": "MLModels/mobilesam-encoder.onnx",
  "DecoderPath": "MLModels/mobilesam-decoder.onnx",
  "MaxUploadBytes": 10485760,
  "MaxImageDimension": 2048,
  "MaxConcurrentEncodes": 2,
  "EmbeddingCacheMinutes": 15,
  "PerIpDailyLimit": 20,
  "GlobalDailyLimit": 500
}
```
Model files (~40–80 MB total) are deployed with the API (not committed to git — downloaded in CI/setup script; add to `.gitignore`).

---

## 7. Data Model Changes

### 7.1 `Product` — new columns (one EF Core migration)

| Column | Type | Notes |
|--------|------|-------|
| `IsVisualizerEnabled` | `bit`, default 0 | Admin opts products in |
| `TextureImagePath` | `nvarchar(500)`, null | **Seamless (tileable)** texture image used for rendering — distinct from the display photo. Required for enabling the product in the visualizer |
| `TextureWidthMeters` | `decimal(18,2)`, null | Real-world width covered by one texture tile (e.g., 1.20). Drives initial scale. Default 1.00 |

### 7.2 New entity `VisualizationRequest` (quota tracking only — no photos, no results)

| Column | Type | Notes |
|--------|------|-------|
| `Id` | int PK | |
| `IpHash` | `nvarchar(64)` | SHA-256(IP + daily salt) — quota counting without storing IPs |
| `Status` | enum Succeeded/Failed | |
| `DurationMs` | int | For admin performance visibility |
| `CreatedAt` | datetime2 | |

Pruned after 90 days by a small background job. (Much slimmer than v1 — there is nothing else to track because nothing is stored.)

---

## 8. Admin Panel Changes

- **Product form**: checkbox «Достъпен във визуализатора»; upload «Текстура за визуализатора (безшевна)» reusing the existing image-upload pipeline; numeric field «Реална ширина на текстурата (м)».
- Validation: enabling the checkbox requires a texture image.
- **Content task for the owner**: each enabled product needs a tileable texture. Guidance doc for the admin (photograph the product top-down, even light; make seamless with a free tool, e.g., GIMP `Filters → Map → Make Seamless`). V1 accepts mild tiling repetition.
- Optional (V1.1): dashboard card „Визуализации днес/месец" from `VisualizationRequest`.

---

## 9. Non-Functional Requirements

| Requirement | Target |
|-------------|--------|
| First segmentation (upload + encoder) | ≤ 6 s p95 on the production server |
| Refinement tap (cached embedding) | ≤ 1 s round-trip |
| Product switch / slider change | < 0.5 s, fully client-side |
| Server resources | CPU-only; ≤ 2 concurrent encodes; ~1–2 GB RAM headroom for model + cache |
| Photo persistence on server | **None** (in-memory processing only); embedding cache ≤ 15 min |
| Mobile | Full flow with touch (tap, brush, drag handles ≥ 44 px touch targets) |
| Browsers | Last 2 versions of Chrome/Firefox/Safari/Edge; WebGL required for primary renderer, canvas-2D fallback |
| Language | 100% Bulgarian UI |

---

## 10. Privacy & Compliance

Dramatically simpler than the generative variant (v1):

1. **No third-party processing** — the photo is processed only by our own API. No DPA with AI vendors, no international transfer, no SCC/adequacy analysis.
2. **No storage** — the photo is processed in memory and never written to disk; only a non-personal image embedding is cached ≤ 15 min; results exist only in the customer's browser. Retention policy is effectively "zero".
3. **Lawful basis** — consent checkbox at upload (§3.2); short privacy text added to the site's privacy/contacts page naming purpose and the no-storage guarantee.
4. **EU AI Act** — Art. 50 synthetic-content marking targets AI-*generated/manipulated* content; here no generative model creates content (segmentation only decides *where* deterministic rendering applies). Marking is likely not required; the „ориентировъчна визуализация" disclaimer is kept for honesty and consumer-protection hygiene. *(Flag for a quick legal sanity check before launch.)*
5. **No biometric/special-category processing** — no face detection of any kind; guidance text discourages people in frame.

---

## 11. Cost & Effort Estimate

**Running cost**: ≈ 0 € per visualization (own-server CPU). No new hosting infrastructure; ~1–2 GB additional RAM on the API host is the only sizing note.

**Build effort (rough)**:

| Work item | Estimate |
|-----------|----------|
| API: ONNX Runtime integration, MobileSAM encoder/decoder pipeline, mask post-processing, embedding cache, endpoints, quotas | 4–5 days |
| Client JS rendering engine: WebGL warp + canvas-2D fallback, texture tiling, mask compositing, luminance transfer, brush/eraser, perspective handles | 6–8 days |
| Blazor UI: Visualizer page (step flow), product side panel, before/after slider, states, mobile layout | 3–4 days |
| Admin: product form fields + validation, texture guidance | 1 day |
| Model export/verification (ONNX), tuning with real photos, cross-browser/mobile testing | 2–3 days |
| **Total** | **~16–21 dev days** |

(Compare: generative variant was ~8–10 days but with per-use fees, third-party transfer, and approximated textures.)

---

## 12. Risks & Limitations

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Segmentation quality on messy terrain (mixed gravel/grass/dirt) | Medium | Tap disambiguation + brush correction; auto-mask is a *starting point* by design |
| Perspective handle UX confuses non-technical users on mobile | Medium | Strong auto-defaults from mask shape; grid visualization; „Пропусни" affordance; usability test with 2–3 real users before launch |
| Realism ceiling — flat-plane + tiling looks synthetic on curved/stepped terrain | Medium | Luminance transfer (biggest lever), feathered edges, disclaimer, before/after slider framing it as a preview |
| Visible texture tiling repetition | Low | Seamless textures required; V1.1: per-tile random rotation |
| ONNX export licensing/compatibility surprises | Low | Verify MobileSAM/SAM2-tiny export license and CPU latency in a 1-day spike **before** committing to the plan (task 0 in the implementation plan) |
| CPU exhaustion under load | Low | Encode semaphore + quotas + 429 backpressure |
| Admin can't produce seamless textures | Low | Written guidance; fallback: plain tiling of a straight-on product photo (acceptable V1 quality) |

---

## 13. Phased Rollout

- **V1 (this spec)**: tap segmentation + brush refinement, perspective handles + scale, instant product switching, before/after, download/add-to-cart, admin texture fields, quotas.
- **V1.1 candidates**: depth-based automatic perspective (Depth Anything small, Apache-2.0) making Step 3 fully automatic; per-tile texture rotation against repetition; admin usage dashboard card; in-browser SAM decoder for zero-latency refinement taps.
- **V2 ideas**: optional generative "photoreal mode" (v1 spec's Option D) as a premium alternative alongside the exact-texture mode; stock scene photos for customers without a usable photo; area (м²) estimation.

---

## 14. Open Questions (for the shop owner)

1. Which paving products should be enabled first, and can top-down texture photos be produced for them? (Blocking for launch content, not for development.)
2. Are the default quotas acceptable (20 photos/day per visitor, 500/day global)?
3. Does the production server have ~2 GB RAM headroom for the model? (Hosting environment is still undecided per `prd.md` §9.)
4. Who reviews the Bulgarian privacy text before go-live?

---

## Appendix A — Key Research Sources (from v1 deep research)

| Source | Used for |
|--------|----------|
| chameleonpower.com/pavers.aspx, get.roomvo.com, renoworks.com | Feature category, commercial proof of photo-upload + product-switch workflow |
| github.com/IDEA-Research/Grounded-Segment-Anything | Text-prompted segmentation pipelines, GPU requirements (informed rejection of self-hosted generative path) |
| winstarstech.medium.com (flooring visualizer) | Classical pipeline precedent: segmentation → contour → perspective warp → LAB luminance transfer |
| github.com/wanadev/perspective.js, Eric-Canas/Homography.js, tulrich.com | Client-side perspective warp feasibility & performance (real-time even on budget phones) |
| medium.com/@geronimo7 (in-browser SAM2) | In-browser segmentation weight sizes (~163 MB) — informed server-side decision |
| learn.microsoft.com — Blazor file uploads / call-web-api | 500 KB default upload limit, `RequestImageFileAsync` client resize, upload security guidance |
| gdprlocal.com, verasafe.com, trilateralresearch.com | GDPR photo handling; EU AI Act Art. 50 scope |

Claim-verification status is recorded in v1 research (15 confirmed 3-0, 9 extracted-unverified, 1 refuted). Pricing claims from v1 are no longer load-bearing in this design.
