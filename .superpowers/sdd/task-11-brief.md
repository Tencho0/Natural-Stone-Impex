### Task 11: Product panel, editing toolbar, perspective handles, compare, actions

**Files:**
- Create: `src/NaturalStoneImpex.Client/Components/VisualizerProductPanel.razor`
- Modify: `src/NaturalStoneImpex.Client/Pages/Public/Visualizer.razor`
- Modify: `src/NaturalStoneImpex.Client/wwwroot/css/app.css` (append)
- Modify: `src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js` (one helper)

**Interfaces:**
- Consumes: everything from Tasks 7–10; `CartService.AddItem(CartItem)` (existing).
- Produces: `<VisualizerProductPanel Products="..." SelectedId="..." Disabled="..." OnSelect="..." />`; JS helper `nsiVisualizer.getStageRect()` → `{ left, top, width, height }` (CSS pixels of the photo element, for handle-drag math).

- [ ] **Step 1: Add the JS helper**

In `src/NaturalStoneImpex.Client/wwwroot/js/visualizer.js`, next to the other `api.*` functions add:

```javascript
  api.getStageRect = function () {
    var r = photoImg.getBoundingClientRect();
    return { left: r.left, top: r.top, width: r.width, height: r.height };
  };
```

- [ ] **Step 2: Create the product panel component**

Create `src/NaturalStoneImpex.Client/Components/VisualizerProductPanel.razor`:

```razor
@using NaturalStoneImpex.Client.Models

<div class="card">
    <div class="card-header">Изберете настилка</div>
    <div class="card-body p-2">
        <input class="form-control form-control-sm mb-2" placeholder="Търсене…"
               value="@_search" @oninput="e => _search = e.Value?.ToString() ?? string.Empty" />
        <select class="form-select form-select-sm mb-2" @bind="_categoryId">
            <option value="0">Всички категории</option>
            @foreach (var category in Products.Select(p => new { p.CategoryId, p.CategoryName }).Distinct())
            {
                <option value="@category.CategoryId">@category.CategoryName</option>
            }
        </select>
        <div class="viz-product-list list-group">
            @foreach (var product in Filtered)
            {
                <button type="button"
                        class="list-group-item list-group-item-action d-flex align-items-center gap-2 @(product.Id == SelectedId ? "active" : "")"
                        disabled="@Disabled"
                        @onclick="() => OnSelect.InvokeAsync(product)">
                    <img src="@(product.ImagePath ?? product.TexturePath)" alt="@product.Name"
                         class="viz-product-thumb" />
                    <span class="flex-grow-1 text-start">@product.Name</span>
                    <span class="text-nowrap">@product.PriceWithVat.ToString("F2") € / @product.UnitDisplay</span>
                </button>
            }
            @if (!Filtered.Any())
            {
                <div class="text-muted small p-2">Няма продукти, отговарящи на търсенето.</div>
            }
        </div>
    </div>
</div>

@code {
    [Parameter] public List<VisualizerProductDto> Products { get; set; } = new();
    [Parameter] public int? SelectedId { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public EventCallback<VisualizerProductDto> OnSelect { get; set; }

    private string _search = string.Empty;
    private int _categoryId;

    private IEnumerable<VisualizerProductDto> Filtered =>
        Products.Where(p =>
            (_categoryId == 0 || p.CategoryId == _categoryId) &&
            (string.IsNullOrWhiteSpace(_search) ||
             p.Name.Contains(_search, StringComparison.OrdinalIgnoreCase)));
}
```

- [ ] **Step 3: Extend the Visualizer page**

In `src/NaturalStoneImpex.Client/Pages/Public/Visualizer.razor`:

1. Add injections at the top (after the existing `@inject` lines):

```razor
@inject CartService CartService
```

2. Replace the workspace markup (the whole `else { ... }` block after the upload card) with a two-column layout:

```razor
else
{
    <div class="row g-3">
        <div class="col-lg-8">
            @if (!_hasMask)
            {
                <div class="alert alert-primary py-2">Докоснете областта, която искате да покриете с настилка.</div>
            }

            <div class="btn-toolbar gap-2 mb-2" role="toolbar" aria-label="Инструменти">
                <div class="btn-group btn-group-sm" role="group">
                    <button class="btn @(ModeButton("tap-add"))" @onclick='() => SetModeAsync("tap-add")' disabled="@_busy">Добави област</button>
                    <button class="btn @(ModeButton("tap-remove"))" @onclick='() => SetModeAsync("tap-remove")' disabled="@(_busy || !_hasMask)">Премахни</button>
                    <button class="btn @(ModeButton("brush"))" @onclick='() => SetModeAsync("brush")' disabled="@_busy">Четка</button>
                    <button class="btn @(ModeButton("erase"))" @onclick='() => SetModeAsync("erase")' disabled="@(_busy || !_hasMask)">Гума</button>
                </div>
                <button class="btn btn-sm btn-outline-danger" @onclick="ClearMaskAsync" disabled="@(_busy || !_hasMask)">Изчисти</button>
                <button class="btn btn-sm @(_showHandles ? "btn-secondary" : "btn-outline-secondary")"
                        @onclick="ToggleHandlesAsync" disabled="@(_busy || !_hasMask)">Перспектива</button>
                @if (_mode is "brush" or "erase")
                {
                    <div class="d-flex align-items-center gap-1">
                        <label class="small text-nowrap" for="viz-brush">Размер на четката</label>
                        <input id="viz-brush" type="range" min="10" max="120" step="5" value="@_brushSize"
                               @oninput="OnBrushSizeChanged" />
                    </div>
                }
            </div>

            <div class="position-relative" @onpointermove="OnHandleMove" @onpointerup="OnHandleUp">
                <div id="viz-stage"></div>
                @if (_showHandles && _photoW > 0)
                {
                    <svg class="viz-handles" viewBox="0 0 @_photoW @_photoH" preserveAspectRatio="none">
                        <polygon points="@HandlePolygon" class="viz-grid-outline" />
                        <line x1="@_corners[0]" y1="@_corners[1]" x2="@_corners[6]" y2="@_corners[7]" class="viz-grid-line" />
                        <line x1="@_corners[2]" y1="@_corners[3]" x2="@_corners[4]" y2="@_corners[5]" class="viz-grid-line" />
                        @for (var i = 0; i < 4; i++)
                        {
                            var index = i;
                            <circle cx="@_corners[index * 2]" cy="@_corners[index * 2 + 1]" r="@(_photoW * 0.02)"
                                    class="viz-handle" @onpointerdown="e => OnHandleDown(e, index)"
                                    @onpointerdown:preventDefault @onpointerdown:stopPropagation />
                        }
                    </svg>
                }
                @if (_busy)
                {
                    <div class="viz-overlay d-flex align-items-center justify-content-center">
                        <div class="text-center text-white">
                            <div class="spinner-border mb-2" role="status"></div>
                            <div>Разпознаваме областта…</div>
                        </div>
                    </div>
                }
            </div>

            @if (_hasMask)
            {
                <div class="row g-2 mt-1">
                    <div class="col-sm-4">
                        <label class="form-label small mb-0" for="viz-scale">Размер на камъка</label>
                        <input id="viz-scale" type="range" class="form-range" min="0.3" max="3" step="0.05"
                               value="@_scale" @oninput="OnScaleChanged" />
                    </div>
                    <div class="col-sm-4">
                        <label class="form-label small mb-0" for="viz-rot">Завъртане</label>
                        <input id="viz-rot" type="range" class="form-range" min="0" max="90" step="1"
                               value="@_rotation" @oninput="OnRotationChanged" />
                    </div>
                    <div class="col-sm-4">
                        <label class="form-label small mb-0" for="viz-cmp">Преди / След</label>
                        <input id="viz-cmp" type="range" class="form-range" min="0" max="100" step="1"
                               value="@_compare" @oninput="OnCompareChanged" />
                    </div>
                </div>

                <div class="d-flex flex-wrap gap-2 mt-2">
                    <button class="btn btn-outline-primary" @onclick="DownloadAsync" disabled="@_busy">Изтегли изображението</button>
                    <button class="btn btn-success" @onclick="AddToCart" disabled="@(_busy || _selected is null)">Добави в количката</button>
                    @if (_selected is not null)
                    {
                        <a class="btn btn-outline-secondary" href="/products/@_selected.Id">Виж продукта</a>
                    }
                </div>
                @if (_cartMessage is not null)
                {
                    <div class="alert alert-success py-2 mt-2">@_cartMessage</div>
                }
            }

            <p class="text-muted small mt-2 mb-1">
                Визуализацията е ориентировъчна. Реалният продукт може да се различава по цвят и вид,
                а размерите са приблизителни.
            </p>

            @if (_error is not null)
            {
                <div class="alert alert-danger mt-2">@_error</div>
            }

            <button class="btn btn-outline-secondary mt-2" @onclick="ResetAsync" disabled="@_busy">Нова снимка</button>
        </div>

        <div class="col-lg-4">
            <VisualizerProductPanel Products="_products"
                                    SelectedId="_selected?.Id"
                                    Disabled="@(_busy || !_hasMask)"
                                    OnSelect="OnProductSelectedAsync" />
        </div>
    </div>
}
```

3. Add the new state fields and methods to `@code`:

```csharp
    private string _mode = "tap-add";
    private int _brushSize = 40;
    private bool _showHandles;
    private double[] _corners = new double[8];
    private int _photoW, _photoH;
    private int _dragIndex = -1;
    private double _scale = 1.0;
    private double _rotation;
    private double _compare;
    private string? _cartMessage;

    private string ModeButton(string mode) =>
        _mode == mode ? "btn-primary" : "btn-outline-primary";

    private string HandlePolygon =>
        $"{_corners[0]},{_corners[1]} {_corners[2]},{_corners[3]} {_corners[4]},{_corners[5]} {_corners[6]},{_corners[7]}";

    private async Task SetModeAsync(string mode)
    {
        _mode = mode;
        await JS.InvokeVoidAsync("nsiVisualizer.setMode", mode);
    }

    private async Task OnBrushSizeChanged(ChangeEventArgs e)
    {
        _brushSize = int.Parse(e.Value?.ToString() ?? "40");
        await JS.InvokeVoidAsync("nsiVisualizer.setBrushSize", _brushSize);
    }

    private async Task ClearMaskAsync()
    {
        await JS.InvokeVoidAsync("nsiVisualizer.clearMask");
        _points.Clear();
        _hasMask = false;
        _showHandles = false;
        await SetModeAsync("tap-add");
    }

    private async Task ToggleHandlesAsync()
    {
        _showHandles = !_showHandles;
        if (_showHandles)
        {
            var corners = await JS.InvokeAsync<double[]>("nsiVisualizer.defaultCornersFromMask");
            if (_corners[2] == 0 && _corners[5] == 0) _corners = corners; // keep user-adjusted values
        }
    }

    private void OnHandleDown(PointerEventArgs e, int index) => _dragIndex = index;

    private async Task OnHandleMove(PointerEventArgs e)
    {
        if (_dragIndex < 0) return;
        var rect = await JS.InvokeAsync<StageRect>("nsiVisualizer.getStageRect");
        _corners[_dragIndex * 2] = Math.Clamp((e.ClientX - rect.Left) / rect.Width * _photoW, 0, _photoW);
        _corners[_dragIndex * 2 + 1] = Math.Clamp((e.ClientY - rect.Top) / rect.Height * _photoH, 0, _photoH);
        await JS.InvokeVoidAsync("nsiVisualizer.setCorners", (object)_corners);
        await JS.InvokeVoidAsync("nsiVisualizer.render");
    }

    private void OnHandleUp(PointerEventArgs e) => _dragIndex = -1;

    private async Task OnScaleChanged(ChangeEventArgs e)
    {
        _scale = double.Parse(e.Value?.ToString() ?? "1", System.Globalization.CultureInfo.InvariantCulture);
        await JS.InvokeVoidAsync("nsiVisualizer.setScale", _scale);
        await JS.InvokeVoidAsync("nsiVisualizer.render");
    }

    private async Task OnRotationChanged(ChangeEventArgs e)
    {
        _rotation = double.Parse(e.Value?.ToString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
        await JS.InvokeVoidAsync("nsiVisualizer.setRotation", _rotation);
        await JS.InvokeVoidAsync("nsiVisualizer.render");
    }

    private async Task OnCompareChanged(ChangeEventArgs e)
    {
        _compare = double.Parse(e.Value?.ToString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
        await JS.InvokeVoidAsync("nsiVisualizer.setCompareRatio", _compare);
    }

    private async Task OnProductSelectedAsync(VisualizerProductDto product)
    {
        _selected = product;
        _cartMessage = null;
        await ApplySelectedProductAsync();
    }

    private async Task DownloadAsync() =>
        await JS.InvokeVoidAsync("nsiVisualizer.downloadResult", "vizualizacia.jpg");

    private void AddToCart()
    {
        if (_selected is null) return;
        CartService.AddItem(new CartItem
        {
            ProductId = _selected.Id,
            ProductName = _selected.Name,
            UnitPriceWithVat = _selected.PriceWithVat,
            VatAmount = _selected.VatAmount,
            UnitPriceWithoutVat = _selected.PriceWithoutVat,
            Unit = _selected.Unit,
            UnitDisplay = _selected.UnitDisplay,
            Quantity = 1,
            ImagePath = _selected.ImagePath
        });
        _cartMessage = "Продуктът е добавен в количката.";
    }

    private record StageRect(double Left, double Top, double Width, double Height);
```

4. In `OnPhotoSelectedAsync` (or `InitStageAsync`), keep the photo dimensions returned by `loadPhotoFromDataUrl` — change the load call in `InitStageAsync` to:

```csharp
        var size = await JS.InvokeAsync<PhotoSize>("nsiVisualizer.loadPhotoFromDataUrl", dataUrl);
        _photoW = size.Width;
        _photoH = size.Height;
```

and add `private record PhotoSize(int Width, int Height);` to `@code`.

5. In `ResetAsync`, also reset the new state: `_mode = "tap-add"; _showHandles = false; _corners = new double[8]; _scale = 1.0; _rotation = 0; _compare = 0; _cartMessage = null;`.

- [ ] **Step 4: Append the styles**

Append to the stylesheet from Task 10 Step 2:

```css
.viz-handles {
    position: absolute;
    inset: 0;
    width: 100%;
    height: 100%;
}
.viz-grid-outline {
    fill: rgba(13, 110, 253, 0.08);
    stroke: rgba(13, 110, 253, 0.9);
    stroke-width: 2;
    vector-effect: non-scaling-stroke;
}
.viz-grid-line {
    stroke: rgba(13, 110, 253, 0.5);
    stroke-width: 1;
    vector-effect: non-scaling-stroke;
}
.viz-handle {
    fill: #0d6efd;
    stroke: #fff;
    stroke-width: 2;
    vector-effect: non-scaling-stroke;
    cursor: grab;
    pointer-events: all;
}
.viz-product-list {
    max-height: 480px;
    overflow-y: auto;
}
.viz-product-thumb {
    width: 48px;
    height: 48px;
    object-fit: cover;
    border-radius: 4px;
}
```

- [ ] **Step 5: Build and verify manually**

`dotnet build`, run API + client, open `/visualizer`, verify: product switching re-renders instantly and highlights the active product; «Премахни» + tap shrinks the mask; brush/eraser edit it; «Перспектива» shows the draggable quad and dragging updates the render live; the three sliders work; «Изтегли» downloads a JPEG; «Добави в количката» updates the cart badge; «Виж продукта» navigates.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): product panel, mask tools, perspective handles, compare and actions"
```

---

