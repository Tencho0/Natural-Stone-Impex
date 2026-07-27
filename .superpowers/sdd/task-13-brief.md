### Task 13: Admin product form — visualizer fields and texture upload

**Files:**
- Modify: `src/NaturalStoneImpex.Client/Services/IProductService.cs`
- Modify: `src/NaturalStoneImpex.Client/Services/ProductService.cs`
- Modify: `src/NaturalStoneImpex.Client/Pages/Admin/ProductForm.razor`

**Interfaces:**
- Consumes: `POST /api/products/{id}/texture` (Task 2); client request models with `IsVisualizerEnabled`/`TextureWidthMeters` (Task 9).
- Produces: `IProductService.UploadTextureAsync(int id, Stream fileStream, string fileName)` → `Task<string?>` (null = success, otherwise Bulgarian error).

- [ ] **Step 1: Client service method**

In `src/NaturalStoneImpex.Client/Services/IProductService.cs` add:

```csharp
    Task<string?> UploadTextureAsync(int id, Stream fileStream, string fileName);
```

In `src/NaturalStoneImpex.Client/Services/ProductService.cs` add (mirrors the existing `UploadImageAsync`, different endpoint and form field name `texture`):

```csharp
    public async Task<string?> UploadTextureAsync(int id, Stream fileStream, string fileName)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        var contentType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "texture", fileName);

        var response = await _httpClient.PostAsync($"api/products/{id}/texture", content);

        if (!response.IsSuccessStatusCode)
        {
            return await ExtractErrorAsync(response);
        }

        return null;
    }
```

- [ ] **Step 2: Product form fields**

Open `src/NaturalStoneImpex.Client/Pages/Admin/ProductForm.razor` and follow its existing structure (the form binds a request model and has an image `InputFile` block — replicate that pattern):

1. In the form markup, after the existing image upload block, add:

```razor
            <hr />
            <h6>Визуализатор</h6>
            <div class="form-check form-switch mb-2">
                <input class="form-check-input" type="checkbox" id="viz-enabled" @bind="_model.IsVisualizerEnabled">
                <label class="form-check-label" for="viz-enabled">Достъпен във визуализатора</label>
            </div>
            <div class="mb-2">
                <label class="form-label" for="viz-width">Реална ширина на текстурата (м)</label>
                <input id="viz-width" type="number" class="form-control" step="0.01" min="0.1" max="100"
                       @bind="_model.TextureWidthMeters" />
            </div>
            @if (_isEdit)
            {
                <div class="mb-2">
                    <label class="form-label">Текстура за визуализатора (безшевна)</label>
                    @if (!string.IsNullOrEmpty(_texturePath))
                    {
                        <div class="mb-1"><img src="@_texturePath" alt="Текстура" style="max-width: 120px;" /></div>
                    }
                    <InputFile OnChange="OnTextureSelected" accept=".jpg,.jpeg,.png" class="form-control" />
                    <div class="form-text">
                        Снимайте продукта отгоре при равномерна светлина. За безшевна текстура използвайте
                        напр. GIMP: Filters → Map → Make Seamless.
                    </div>
                </div>
            }
            else
            {
                <div class="form-text mb-2">Текстурата се качва след създаване на продукта (в режим на редакция).</div>
            }
```

`_model` here is the form's bound request object (`CreateProductRequest`/`UpdateProductRequest` — adapt to the actual field name used in the file). `_isEdit` is the form's existing create/edit flag (adapt to the actual name).

2. In `@code`, add texture state and handler, and wire the upload into the existing save flow the same way the image upload is wired (after a successful create/update, if a texture file was chosen, upload it and surface any error through the form's existing error display):

```csharp
    private IBrowserFile? _textureFile;
    private string? _texturePath; // set from the loaded ProductDto.TextureImagePath (resolve like the image path)

    private void OnTextureSelected(InputFileChangeEventArgs e) => _textureFile = e.File;

    private async Task<string?> UploadTextureIfSelectedAsync(int productId)
    {
        if (_textureFile is null) return null;
        await using var stream = _textureFile.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024);
        return await ProductService.UploadTextureAsync(productId, stream, _textureFile.Name);
    }
```

3. When loading an existing product into the form, populate `_model.IsVisualizerEnabled`, `_model.TextureWidthMeters`, and `_texturePath` from the `ProductDto` fields (Task 9).

- [ ] **Step 3: Build and verify manually**

`dotnet build`; run both projects; in `/admin` edit a product: upload a texture, set width 1.2, tick «Достъпен във визуализатора», save. Verify `GET /api/visualizer/products` now returns it and it appears in the visualizer's product panel. Also verify the guard: on a product without texture, ticking the switch and saving shows «За да включите продукта във визуализатора, първо качете текстура.» (upload happens after save on create — enable requires a second save, which the guard message makes clear).

- [ ] **Step 4: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): admin product form texture and visualizer fields"
```

---

