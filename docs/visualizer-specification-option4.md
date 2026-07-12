# Functional & Technical Specification — Visualizer «Фотореалистичен режим» (Option 4: Full Generative Edit)

**Status**: Draft v1 — companion track to `visualizer-specification.md` (Option 2), which is the approach being implemented **first**. This document specifies Option 4 (full generative AI editing) as an **additive second mode** on the same visualizer page, to be built later so the two approaches can be compared on real photos.
**Depends on**: `visualizer-specification.md` (Option 2 — provides the page, upload flow, product panel, quota table), `technical-specification.md`, `conventions.md`
**Implementation plan**: `docs/plans/visualizer-option4-implementation-plan.md`

---

## 1. Feature Overview

In generative mode, the customer's uploaded photo and the selected product's images are sent to a hosted instruction-based image-editing model (Gemini Flash Image class) with the instruction "replace the ground surface with this paving stone." The model handles segmentation, perspective, lighting, shadows, and occlusion **implicitly** and returns a fully edited photograph. Switching products re-runs the edit (~3–20 s), with results cached per (photo, product).

### 1.1 Relationship to Option 2 (the mode being built first)

Option 4 is **not** a replacement — it is a second rendering mode on the same `/visualizer` page:

- Mode toggle at the top of the page: **«Точна текстура»** (Option 2, default) / **«Фотореалистичен (AI)»** (Option 4).
- Shared: photo upload + client-side downscale, product side panel, before/after slider, add-to-cart actions, `IsVisualizerEnabled` + `TextureImagePath` product fields, `VisualizationRequest` quota table.
- Generative-mode-specific: its own consent text (third-party processing), server-side photo storage with 48 h retention, provider adapter + costs, AI-generated labeling.

### 1.2 The two modes side by side

| Property | Option 2 «Точна текстура» | Option 4 «Фотореалистичен (AI)» |
|----------|---------------------------|--------------------------------|
| Texture fidelity | Exact product texture | AI approximation guided by reference images |
| Realism (perspective, shadows, occlusion) | Approximated (plane + luminance transfer) | High — model renders scene-consistent result |
| Customer effort | Tap area + optionally adjust grid | None — upload and pick a product |
| Product switch | Instant (<0.5 s, local) | 3–20 s per new product (cached thereafter) |
| Per-use cost | ~0 € | ~$0.04–0.16 per generation |
| Photo leaves our infrastructure | Never | Yes — sent to the AI provider (GDPR §10) |
| EU AI Act Art. 50 marking | Likely not triggered | **Required** — visible label + machine-detectable mark |

### 1.3 Why keep both

The comparison is the point: the owner wants to evaluate exact-texture fidelity against generative realism on real customer photos before deciding which mode (or both) stays long-term. §13 defines the comparison protocol.

---

## 2. Goals & Non-Goals

### Goals
1. One-click photorealistic re-paving of the customer photo — no masking or perspective input from the customer.
2. Product switching regenerates the visualization; already-generated combinations return instantly from cache.
3. Per-use cost capped by per-IP and global daily limits; worst-case daily spend is a fixed, configurable ceiling.
4. Full GDPR compliance for third-party processing; EU AI Act labeling on every generated image.
5. Reuse of the Option 2 page, panel, and schema — one visualizer, two modes.

### Non-Goals (V1 of this mode)
- Mask-constrained inpainting (customer-drawn region limiting the AI edit) — V1.1 candidate (§14).
- Pixel-exact product reproduction — this mode trades exactness for realism by design; the disclaimer covers it.
- Provider fine-tuning or self-hosted generative models.
- Area/quantity estimation, AR, customer accounts.

---

## 3. Functional Specification (deltas from Option 2)

### 3.1 Mode toggle

- Two-button toggle above the canvas: «Точна текстура» | «Фотореалистичен (AI)». Default: «Точна текстура».
- Switching modes keeps the uploaded photo. Mask/perspective state from Option 2 mode is retained but unused in generative mode.
- The generative mode button is hidden when `Visualizer:Generative:Enabled` is `false` — the page then behaves exactly as Option 2.

### 3.2 Generative-mode flow

**Step 1 — Consent (first generative use per session)**
Generative mode requires its own consent (Option 2's consent does not cover third-party transfer):
> „Съгласен/на съм снимката ми да бъде изпратена за обработка към външна AI услуга (Google) с цел генериране на визуализация. Снимката и резултатите се изтриват автоматично до 48 часа."

Checkbox + link to the privacy text. Without it, «Генерирай» is disabled.

**Step 2 — Generate**
- Button «Генерирай визуализация» (or automatic on product select once a photo + consent exist).
- Progress state: spinner + „Генерираме фотореалистична визуализация… Обикновено отнема 5–20 секунди."
- On success: result replaces the canvas; before/after slider works against the original photo (same shared component as Option 2 mode).

**Step 3 — Product switching**
- Selecting another product triggers a new generation using the **stored photo token** (no re-upload).
- Previously generated combinations load instantly (server-side result cache, §5.4).
- The side panel shows a small clock icon on products not yet generated in this session — communicating that switching costs a wait, unlike the exact-texture mode.

### 3.3 Labels on the result (mandatory, generative mode only)

1. Visible badge on the result image: **„Генерирано с изкуствен интелект"** — EU AI Act Art. 50 (applies from 02.08.2026).
2. Machine-detectable marking: Gemini output carries Google's SynthID watermark; **do not** re-encode in a way documented to strip it, and verify equivalent marking if the provider changes.
3. Shared disclaimer (both modes): „Визуализацията е ориентировъчна. Реалният продукт може да се различава по цвят и вид."

### 3.4 States & errors (generative-mode additions)

| Situation | Behavior |
|-----------|----------|
| Provider error / timeout (60 s) | „Визуализацията не можа да бъде генерирана. Моля, опитайте отново." + retry; failure logged in `VisualizationRequest` |
| Per-IP daily limit reached | „Достигнахте дневния лимит за AI визуализации. Опитайте отново утре — или използвайте режим «Точна текстура»." |
| Global daily budget cap reached | „AI режимът е временно недостъпен. Режим «Точна текстура» работи без ограничение." |
| Photo token expired (>48 h) | „Сесията е изтекла. Моля, качете снимката отново." |
| Generative mode disabled by config | Toggle hidden; page = Option 2 only |

Note the deliberate pattern: **every generative failure path points the customer to the always-available exact-texture mode.**

---

## 4. System Architecture

```
┌────────────────────────────────┐        ┌──────────────────────────────────┐        ┌──────────────────┐
│ Blazor WASM Client             │        │ NaturalStoneImpex.Api            │        │ AI Provider      │
│ /visualizer page               │        │                                  │        │ (Gemini API)     │
│  - mode toggle                 │ HTTPS  │ VisualizerController             │ HTTPS  │                  │
│  - photo downscale (shared)    ├───────►│  POST /api/visualizer/generate   ├───────►│ POST /v1beta/    │
│  - product panel (shared)      │◄───────┤  → { photoToken, imageUrl }      │◄───────┤  interactions    │
│  - before/after (shared)       │        │                                  │        │                  │
│                                │        │ GenerativeVisualizationService   │        │ output_image     │
│ Option 2 renderer (local)      │        │  - validate/resize/strip EXIF    │        └──────────────────┘
│ lives beside this — untouched  │        │  - store photo under photoToken  │
└────────────────────────────────┘        │  - quotas + budget cap           │
                                          │  - result cache (disk)           │
                                          │ GeminiImageEditProvider          │
                                          │ VisualizerCleanupService (48 h)  │
                                          └──────────────────────────────────┘
```

**Why server-mediated**: Blazor WASM `HttpClient` is subject to browser CORS (AI APIs don't allow arbitrary browser origins), and the provider API key must never ship in the WASM payload. Microsoft's documented remedy is exactly this proxy pattern. The server is also the only place quotas and the budget cap can be enforced trustworthily.

**Contrast with Option 2**: Option 2 stores nothing (in-memory segmentation, rendering in the browser). Generative mode must persist the photo (for token-based product switching) and the results (for caching) — hence the 48 h retention + cleanup service that Option 2 does not need.

---

## 5. Detailed Design

### 5.1 Provider abstraction

```csharp
public interface IImageEditProvider
{
    Task<ImageEditResult> EditAsync(ImageEditInput input, CancellationToken ct);
}

public record ImageEditInput(
    byte[] CustomerPhoto, string CustomerPhotoMimeType,
    List<(byte[] Bytes, string MimeType)> ReferenceImages,
    string Prompt);

public record ImageEditResult(byte[] ImageBytes, string MimeType);
```

One implementation per provider. Primary: `GeminiImageEditProvider`. The provider name is config-driven; adding fal.ai/FLUX later means one new adapter class, nothing else changes.

### 5.2 Gemini adapter (primary provider)

- Endpoint: `POST https://generativelanguage.googleapis.com/v1beta/interactions`, header `x-goog-api-key: {key}` (API shape as of 07.2026 — the "interactions" API; re-verify at implementation).
- Request body: `model` (config, default `gemini-3.1-flash-image`), `input` array of parts — one `{"type":"text","text": prompt}` followed by `{"type":"image","mime_type":"image/jpeg","data":"<base64>"}` parts (customer photo first, then reference images), and `"response_format": {"type":"image"}`.
- Response: `output_image: { data: <base64>, mime_type }`.
- Errors: non-2xx or missing `output_image` → typed `ImageEditProviderException` → 502 with the standard Bulgarian error.
- Researched price/latency class: ~$0.06–0.16 per image depending on output resolution; 1–3 s generation for the Flash tier (plus network). EU processing: prefer the Vertex AI EU-region endpoint variant if/when configured (§10).

### 5.3 Prompt construction

Built server-side (English — model-facing, not UI):

> "The first image is a customer's photo of their outdoor property. The second image is the texture of a paving stone product{ and the third image shows the same product}. Replace the ground surface (driveway, path or yard area) in the first image with this paving stone. Preserve the camera perspective, lighting, shadows, and every other object in the scene — buildings, cars, plants, people. Lay the paving at a realistic scale with visible joints. {VisualizerPromptHint}. Return only the edited photograph."

Reference images sent: `TextureImagePath` (required for enabled products — same asset Option 2 uses for rendering) and `ImagePath` (the display photo) when present. `VisualizerPromptHint` is a new optional per-product English hint (e.g., *"irregular grey-beige gneiss slabs with wide joints"*).

### 5.4 Photo & result storage (capability-URL pattern)

- Photo intake: validate by **content** (decode with ImageSharp — rejects disguised files), enforce size server-side, re-encode to JPEG (quality 85, max 2048 px) — re-encoding also strips EXIF/GPS metadata (`ExifProfile = null`).
- Stored under `wwwroot/uploads/visualizer/{photoToken}/original.jpg`; results as `{photoToken}/{productId}.jpg`. `photoToken` = GUID → unguessable capability URL; results are served by the existing static-files middleware, no extra endpoint.
- Result cache = the file's existence: a repeat (photoToken, productId) request returns the existing URL without a provider call.
- `VisualizerCleanupService` (`BackgroundService`, hourly): deletes token folders older than `RetentionHours` (48) and prunes `VisualizationRequest` rows older than 90 days.

### 5.5 Quotas & budget cap (denial-of-wallet protection)

Two layers, both enforced server-side before any provider call:
1. **Burst**: ASP.NET Core rate limiter, per-IP fixed window (default 5/min) on the generate endpoint.
2. **Daily**: `VisualizationRequest` counts — per-IP (`IpHash` = SHA-256(IP + date + salt), default 10/day) and global (default 200/day). Worst-case daily spend = `GlobalDailyLimit × price` ≈ 200 × $0.07 = **$14/day hard ceiling**.

Failed generations are recorded (`Status = Failed`) but still count toward quotas (prevents retry-hammering).

---

## 6. API Design

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/visualizer/generate` | Multipart: `photo` (first call) **or** `photoToken` (switches), + `productId`. Runs the generative edit | No |
| GET | `/uploads/visualizer/{photoToken}/{productId}.jpg` | Result image (static files, capability URL) | No |

(`GET /api/visualizer/products` is shared with Option 2 and already exists.)

**Response (200)**:
```json
{ "photoToken": "b7e2…", "productId": 5, "imageUrl": "/uploads/visualizer/b7e2…/5.jpg", "cached": false }
```

Errors: standard `{ "error": "…" }` — 400 (validation/consent), 404 (unknown/expired token, unknown product), 429 (quota/budget), 502 (provider failure).

### 6.1 Configuration (nested under the existing `Visualizer` section)

```json
"Visualizer": {
  "Generative": {
    "Enabled": false,
    "Provider": "Gemini",
    "Model": "gemini-3.1-flash-image",
    "ApiKey": "",                    // dev: user-secrets; prod: environment variable. NEVER committed.
    "MaxUploadBytes": 10485760,
    "MaxImageDimension": 2048,
    "BurstPerMinute": 5,
    "PerIpDailyLimit": 10,
    "GlobalDailyLimit": 200,
    "RetentionHours": 48,
    "ProviderTimeoutSeconds": 60,
    "EstimatedCostPerImage": 0.07
  }
}
```

`Enabled: false` by default — Option 2 ships first; flipping the flag (plus the API key) turns the mode on.

---

## 7. Data Model Changes (additive migration on top of Option 2's)

### 7.1 `Product` — one new column

| Column | Type | Notes |
|--------|------|-------|
| `VisualizerPromptHint` | `nvarchar(500)`, null | Optional English product description injected into the prompt (§5.3) |

(`IsVisualizerEnabled`, `TextureImagePath`, `TextureWidthMeters` already exist from Option 2.)

### 7.2 `VisualizationRequest` — extended for generative tracking

| New column | Type | Notes |
|------------|------|-------|
| `Mode` | int enum: `Segmentation = 0`, `Generative = 1` | Existing Option 2 rows default to 0 |
| `PhotoToken` | `nvarchar(64)`, null | Generative only; indexed |
| `ProductId` | int, null, FK → Product (`Restrict`) | Generative only |
| `ProviderCostEstimate` | `decimal(18,4)`, null | From `EstimatedCostPerImage` — powers the admin cost counter |

One table serves both modes; the admin dashboard can compare usage per mode directly (useful for §13).

---

## 8. Admin Panel Changes

- **Product form**: one new optional field «AI описание (на английски, за фотореалистичния режим)» → `VisualizerPromptHint`. Everything else (visualizer checkbox, texture upload) already exists from Option 2.
- **Dashboard (V1.1)**: „AI визуализации днес/месец" count + estimated cost from `VisualizationRequest` where `Mode = Generative`.

---

## 9. Non-Functional Requirements (generative mode)

| Requirement | Target |
|-------------|--------|
| Generation latency | p95 ≤ 20 s end-to-end; provider timeout 60 s |
| Cached result | ≤ 1 s |
| Cost ceiling | `GlobalDailyLimit × EstimatedCostPerImage` — fixed, configurable (default ≈ $14/day) |
| Availability | Independent feature flag; all failures degrade to Option 2 mode |
| Photo retention | ≤ 48 h server-side, automatic deletion |
| Mobile | Same flow; progress state must survive screen lock/tab switch (re-request by token is idempotent via cache) |
| Language | 100% Bulgarian UI |

---

## 10. Privacy & Compliance (the material delta vs Option 2)

Option 2's "no third party, no storage" analysis does **not** cover this mode. Generative mode additionally requires:

1. **Separate consent** naming the third-party processing (§3.2 Step 1), logged with the `VisualizationRequest` row.
2. **Processor due diligence**: DPA with Google (covered by Google's standard API terms — verify the paid tier's data-use terms state inputs are not used for training; the free tier's terms differ and are **not acceptable**).
3. **International transfer**: EU-US Data Privacy Framework / SCCs apply for US processing. Preferred mitigation: Vertex AI EU-region endpoint so photos stay in the EU. Decision recorded before enabling the flag.
4. **Transparency**: privacy text updated to name the provider, purpose, 48 h retention, and data-subject rights.
5. **EU AI Act Art. 50** (in force from 02.08.2026): visible „Генерирано с изкуствен интелект" label + machine-detectable marking (SynthID via Gemini; re-verify if provider changes).
6. **Data minimization**: EXIF/GPS stripped at intake; photos auto-deleted at 48 h; no raw IPs stored (salted hash).

---

## 11. Cost & Effort Estimate

**Running cost**: $0.04–0.16 per generation (model/resolution dependent). Realistic 5–30 generations/day → **$10–100/month**, hard-capped by config. Zero when the flag is off.

**Build effort on top of an implemented Option 2** (shared page, panel, upload, slider, quota table already exist):

| Work item | Estimate |
|-----------|----------|
| API: provider adapter + generation service + endpoints + quotas/budget + cleanup + migration | 3–4 days |
| Client: mode toggle, generative flow states, consent variant, panel wait indicators | 1.5–2 days |
| Admin field, privacy texts, AI labeling, config, testing with real photos | 1–1.5 days |
| **Total** | **~6–8 dev days** |

---

## 12. Risks & Limitations

| Risk | Severity | Mitigation |
|------|----------|-----------|
| AI result unfaithful to the exact product (color/pattern drift) | Medium | Texture + photo as references; prompt hints; disclaimer; per-product admin preview before enabling; §13 comparison decides if acceptable |
| Quality collapse when paved area dominates the photo (>~50%) | Medium | Upload guidance; retry; known limitation |
| Provider API churn (the "interactions" API is new as of 2026) | Medium | Adapter isolation; re-verify request shape at implementation; pin model version in config |
| Prompt-injection-adjacent surprises (model edits things it shouldn't) | Low–Medium | Strict prompt; V1.1 mask-constrained inpainting eliminates this class |
| Cost runaway | Low | Burst limit + daily per-IP + global cap; failed calls also counted |
| Provider outage | Low | 502 → UI degrades to Option 2 mode |
| GDPR complaint about third-party transfer | Low | §10 controls; EU-region endpoint preference; flag stays off until DPA/transfer check is done |

---

## 13. Comparison Protocol (the reason this mode exists)

When both modes are live (generative behind the flag, enabled for testing):

1. **Test set**: 10–15 real photos (owner + friendly customers): driveways, garden paths, terraces; sunny/overcast; flat/sloped; with/without obstacles (car, pots).
2. **Per photo × 3 representative products**, produce both modes' results (the shared page makes this trivial — toggle the mode, same photo, same product).
3. **Score sheet** (owner + 2–3 impartial people, blind if possible): realism (1–5), product recognizability — „бих познал, че това е този камък" (1–5), would-share-with-spouse test (yes/no).
4. **Operational data** from `VisualizationRequest`: latency, failure rate, cost per session, mode usage split if exposed to real customers.
5. **Decision**: keep both modes, keep one, or make generative a paid/limited "premium preview". Criteria: if generative scores ≥1.5 points higher on realism **and** product recognizability stays ≥4, generative becomes the default; if product recognizability drops below 3, exact-texture stays primary.

---

## 14. Open Questions (for the shop owner, before enabling the flag)

1. Confirm Google as the provider (or require an EU-only processing path via Vertex AI from day one)?
2. Budget ceiling: is the $14/day default cap right, or lower for the comparison phase (e.g., 50/day global)?
3. Who signs off the DPA/data-use verification and the updated privacy text?
4. Should generative mode be exposed to real customers during the comparison phase, or owner-only (feature flag + admin-session check) until the §13 decision?

---

## Appendix A — Research base

Shared with `visualizer-specification.md` Appendix A (deep research of 05.07.2026: 15 claims adversarially confirmed, 9 extracted-unverified, 1 refuted). Additional Option 4-specific sources: Gemini API image-generation docs (`ai.google.dev/gemini-api/docs/image-generation`, "interactions" API, 07.2026); researched per-image pricing $0.01–0.16 across FLUX Kontext / Gemini Flash Image / GPT Image / Stability (unverified snapshot — re-check at implementation).
