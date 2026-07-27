# Task 13 Report: Admin Product Form — Visualizer Fields and Texture Upload

## Status
✅ COMPLETE

## Commit
- **Commit Hash**: `83c931a`
- **Branch**: `feature/visualizer`
- **Message**: `feat(visualizer): admin product form texture and visualizer fields`

## Adaptation From Brief to Real Structure

The brief's snippets assumed the form binds directly to `CreateProductRequest`/`UpdateProductRequest` (`_model`) and a flag `_isEdit`. The real file is different in three load-bearing ways, and the implementation was adapted accordingly:

1. **Bound model is a local view-model, not the request DTO.** `ProductForm.razor` binds `EditForm Model="_formModel"` to a private `sealed class ProductFormModel` (declared at the bottom of the file) — it is *not* `CreateProductRequest`/`UpdateProductRequest` directly. The two new fields (`IsVisualizerEnabled`, `TextureWidthMeters`) were added to `ProductFormModel`, then explicitly copied into both the `createRequest` and `updateRequest` object initializers inside `HandleSave()` (mirroring how every other field, e.g. `StockQuantity`, is already copied across). `TextureWidthMeters` defaults to `1.00m` on `ProductFormModel`, matching the client request models' default.
2. **Create/edit flag is `_isEditMode`** (`ProductId.HasValue`), not `_isEdit`. Used directly in the new markup (`@if (_isEditMode)`) and in the save-flow texture-upload guard.
3. **Layout is card-sectioned, not a flat block.** The real form has three `card` sections in the left column (`col-lg-8`: Basic Info, Pricing, Stock & Unit) and one sticky `card` in the right column (`col-lg-4`: Снимка/Image). The brief's flat `<hr/><h6>` snippet didn't fit this. I added a **4th card section, "Визуализатор"**, in the left column after "Наличност" (Stock & Unit), styled identically to the other three sections (`card mb-4`, `card-header` with `bi-*` icon in `var(--nsi-accent)`, `card-body` with `padding: 1.5rem`). Used `bi-eye` as the header icon (no existing icon was reserved for this concept). The checkbox uses a raw `<input type="checkbox" class="form-check-input">` (not an `InputCheckbox` component) because that is the only precedent in this codebase for boolean binding (`Visualizer.razor` line 36 uses the same raw pattern — there is no `InputCheckbox` usage anywhere). The width field uses `InputNumber` + `ValidationMessage` (not a raw `<input>`) to match every other numeric field in this exact form (`PriceWithoutVat`, `StockQuantity`, etc. all use `InputNumber`/`ValidationMessage`); `min="0.1" max="100" step="0.01"` splat straight through onto the rendered `<input>` exactly as `step="0.01"` already does for the price fields.

New state fields and the selection handler follow the brief's semantics but use codebase-consistent names instead of the brief's literal placeholder names (which were not "adapt to the real name" instructions like `_model`/`_isEdit` were — they were new symbols, so I aligned them to the file's existing image-upload naming convention):
- `_selectedImage`/`_imageError`/`_existingImagePath` (existing) → `_selectedTexture`/`_textureError`/`_existingTexturePath` (new), instead of the brief's `_textureFile`/`_texturePath`.
- `OnTextureSelected` mirrors `OnImageSelected`'s validation (extension allowlist `.jpg/.jpeg/.png`, 5 MB size cap, Bulgarian error strings reused verbatim from the image handler) — the brief's version had no validation. Added for consistency with the form's established pattern; not required, but no live base64 preview-on-select was added (unlike the image field) because the form always navigates away (`Navigation.NavigateTo("/admin/products")`) immediately after a successful save, so a live preview would never be seen.

## Changes Made

### 1. `src/NaturalStoneImpex.Client/Services/IProductService.cs`
Added to the interface, right after `UploadImageAsync`:
```csharp
Task<string?> UploadTextureAsync(int id, Stream fileStream, string fileName);
```

### 2. `src/NaturalStoneImpex.Client/Services/ProductService.cs`
- Added `UploadTextureAsync(int id, Stream fileStream, string fileName)` — copy of `UploadImageAsync`'s multipart logic, posting to `api/products/{id}/texture` with form field name `"texture"` (per Task 2's API contract).
- **Additional fix beyond the brief's literal snippet, required by the task's own instructions** ("resolve URL like the image path is resolved"): `GetByIdAsync` previously only resolved `product.ImagePath` via `ResolveImageUrl`. Added the same resolution for `product.TextureImagePath`:
```csharp
if (product != null)
{
    product.ImagePath = ResolveImageUrl(product.ImagePath);
    product.TextureImagePath = ResolveImageUrl(product.TextureImagePath);
}
```
Without this, the texture preview in edit mode would render a relative path and 404 (the image preview only works today because this resolution already existed for `ImagePath`). `ProductListDto`/`GetAllAsync`/`GetLowStockAsync` were left untouched — that DTO has no `TextureImagePath` field, so no change was needed there.

### 3. `src/NaturalStoneImpex.Client/Pages/Admin/ProductForm.razor`
- **Markup**: new "Section 4: Визуализатор" card (lines 151–195) in the left column, containing:
  - Switch: «Достъпен във визуализатора» bound to `_formModel.IsVisualizerEnabled`.
  - Number field: «Реална ширина на текстурата (м)» bound to `_formModel.TextureWidthMeters`, `InputNumber` with `step="0.01" min="0.1" max="100"` + `ValidationMessage`.
  - Edit mode (`_isEditMode`): «Текстура за визуализатора (безшевна)» label, existing-texture preview image (shown only if `_existingTexturePath` is non-empty), `InputFile` (`.jpg,.jpeg,.png`), the exact GIMP guidance text, and an inline `_textureError` display.
  - Create mode: the exact note «Текстурата се качва след създаване на продукта (в режим на редакция).»
- **`@code`**: added `_textureError`, `_existingTexturePath`, `_selectedTexture` fields; populated `_formModel.IsVisualizerEnabled`/`_formModel.TextureWidthMeters`/`_existingTexturePath` from the loaded `ProductDto` in `OnInitializedAsync`; added `OnTextureSelected` validation handler; added `IsVisualizerEnabled`/`TextureWidthMeters` to both `createRequest` and `updateRequest` initializers in `HandleSave`; added a texture-upload block right after the existing image-upload block, gated on `_isEditMode && _selectedTexture is not null`, surfacing any error through the existing `_errorMessage` (top-of-page alert) exactly like the image upload does.
- **`ProductFormModel`**: added `IsVisualizerEnabled` (bool) and `TextureWidthMeters` (decimal, default `1.00m`, `[Range(0.1, 100)]` with a Bulgarian validation message consistent with the other Range-validated fields in this class).

## Build & Test Results

### Build
```
dotnet build
```
- **Status**: SUCCEEDED (clean)
- **Warnings**: 0
- **Errors**: 0
- **Duration**: ~9.6s
- Projects: NaturalStoneImpex.Api, NaturalStoneImpex.Api.Tests, NaturalStoneImpex.Client (incl. Blazor WASM output)

### Tests
```
dotnet test
```
- **Status**: ALL PASSED
- **Total**: 19, Passed: 19, Failed: 0, Skipped: 0, Duration: ~3s

No new client-side tests exist for this project (no Blazor component test harness in the repo), so the 19 tests are the pre-existing API test suite — unaffected by this client-only change, confirming no regression.

## Self-Review Findings

- **Fields bound to the actual model object?** Yes — `_formModel.IsVisualizerEnabled` / `_formModel.TextureWidthMeters` on `ProductFormModel`, the object actually passed to `<EditForm Model="_formModel">`.
- **Both create and edit request models carry the fields on save?** Yes — both `createRequest` and `updateRequest` object initializers in `HandleSave()` now set `IsVisualizerEnabled` and `TextureWidthMeters`.
- **Texture upload fires after successful save (edit mode), errors surfaced through the form's existing error display?** Yes — placed immediately after the image-upload block, guarded by `_isEditMode && _selectedTexture is not null`, and on failure sets `_errorMessage` (rendered by the pre-existing top-of-page `alert-danger` block) and returns, exactly matching the image-upload error path.
- **Preview shows existing texture with resolved absolute URL?** Yes — `_existingTexturePath` is set from `product.TextureImagePath`, which is now resolved through `ResolveImageUrl` in `ProductService.GetByIdAsync` (this resolution did not exist before this task and was added as part of it).
- **All Bulgarian strings byte-exact?** Verified via grep against the brief's required strings — all five match exactly: «Достъпен във визуализатора», «Реална ширина на текстурата (м)», «Текстура за визуализатора (безшевна)», the GIMP guidance sentence, «Текстурата се качва след създаване на продукта (в режим на редакция).»

## Files Changed
1. `src/NaturalStoneImpex.Client/Services/IProductService.cs` — +1 line (new method signature)
2. `src/NaturalStoneImpex.Client/Services/ProductService.cs` — +23 lines (new `UploadTextureAsync` method) / +2 lines modified (`TextureImagePath` resolution in `GetByIdAsync`)
3. `src/NaturalStoneImpex.Client/Pages/Admin/ProductForm.razor` — new Visualizer card section, new `@code` state/handler/wiring, new `ProductFormModel` fields

**Total**: 3 files, 127 insertions(+), 2 deletions(-)

## Concerns

- **No live browser verification** was performed (no SQL Server in this environment, per task constraints) — this task's Step 3 manual verification (upload texture, set width 1.2, tick the switch, save, confirm it surfaces via `GET /api/visualizer/products`, and confirm the "no texture" guard message on save) is deferred to Task 14's E2E pass, as instructed.
- I diverged from the brief's literal field names (`_textureFile`/`_texturePath`) in favor of the codebase's existing naming convention (`_selectedTexture`/`_existingTexturePath`/`_textureError`) and added client-side extension/size validation the brief didn't specify, to keep the new code indistinguishable in style from the adjacent image-upload code it mirrors. Semantics and wiring order match the brief exactly.
- The 4th card section and its icon (`bi-eye`) are a judgment call — the brief assumed a flatter layout that doesn't exist in this form; no other icon was reserved for "visualizer" elsewhere in the codebase to anchor to.
