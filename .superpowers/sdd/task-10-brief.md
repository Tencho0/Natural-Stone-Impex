### Task 10: Visualizer page — upload, tap-to-segment, render (happy path)

**Files:**
- Create: `src/NaturalStoneImpex.Client/Pages/Public/Visualizer.razor`
- Modify: `src/NaturalStoneImpex.Client/wwwroot/css/app.css` (append; if the stylesheet has a different name, use the one `index.html` references)

**Interfaces:**
- Consumes: `IVisualizerService` (Task 9), `window.nsiVisualizer` API (Tasks 7–8).
- Produces: `/visualizer` route; `[JSInvokable] OnCanvasTapAsync(double x, double y, int label)` and `[JSInvokable] OnMaskEditedAsync()` (names must match the JS calls from Task 8). Accepts query string `?productId=N` (used by Task 12). Task 11 extends this file.

- [ ] **Step 1: Create the page**

Create `src/NaturalStoneImpex.Client/Pages/Public/Visualizer.razor`:

```razor
@page "/visualizer"
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.WebUtilities
@using NaturalStoneImpex.Client.Models
@using NaturalStoneImpex.Client.Services
@inject IVisualizerService VisualizerService
@inject NavigationManager Navigation
@inject IJSRuntime JS
@implements IAsyncDisposable

<PageTitle>Визуализатор — Natural Stone Impex</PageTitle>

<h1 class="mb-2">Визуализатор</h1>
<p class="text-muted">Качете снимка на вашия двор или алея и вижте как ще изглежда с нашите настилки.</p>

@if (_products is null)
{
    <div class="text-center my-5">
        <div class="spinner-border" role="status"><span class="visually-hidden">Зареждане…</span></div>
    </div>
}
else if (_products.Count == 0)
{
    <div class="alert alert-info">Визуализаторът не е наличен в момента.</div>
}
else if (!_photoLoaded)
{
    <div class="card mx-auto" style="max-width: 640px;">
        <div class="card-body">
            <h5 class="card-title">Качване на снимка</h5>
            <p class="card-text">
                Снимайте площта така, че да се вижда цялата повърхност, която искате да покриете.
                Избягвайте хора в кадъра.
            </p>
            <div class="form-check mb-3">
                <input class="form-check-input" type="checkbox" id="viz-consent" @bind="_consent">
                <label class="form-check-label" for="viz-consent">
                    Съгласен/на съм снимката да бъде обработена на сървъра на магазина за целите на
                    визуализацията. Снимката се изтрива автоматично след обработката.
                </label>
            </div>
            <InputFile OnChange="OnPhotoSelectedAsync" accept="image/*" capture="environment"
                       class="form-control" disabled="@(!_consent || _busy)" />
            @if (_error is not null)
            {
                <div class="alert alert-danger mt-3 mb-0">@_error</div>
            }
        </div>
    </div>
}
else
{
    <div class="mb-2">
        @if (!_hasMask)
        {
            <div class="alert alert-primary py-2">Докоснете областта, която искате да покриете с настилка.</div>
        }
    </div>

    <div class="position-relative">
        <div id="viz-stage"></div>
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

    <p class="text-muted small mt-2 mb-1">
        Визуализацията е ориентировъчна. Реалният продукт може да се различава по цвят и вид,
        а размерите са приблизителни.
    </p>

    @if (_error is not null)
    {
        <div class="alert alert-danger mt-2">@_error</div>
    }

    <button class="btn btn-outline-secondary mt-2" @onclick="ResetAsync" disabled="@_busy">Нова снимка</button>
}

@code {
    private List<VisualizerProductDto>? _products;
    private VisualizerProductDto? _selected;
    private bool _consent;
    private bool _photoLoaded;
    private byte[]? _photoBytes;
    private string? _sessionToken;
    private readonly List<SegmentPoint> _points = new();
    private bool _hasMask;
    private bool _busy;
    private string? _error;
    private bool _stageInitialized;
    private DotNetObjectReference<Visualizer>? _selfRef;

    protected override async Task OnInitializedAsync()
    {
        _products = await VisualizerService.GetProductsAsync();

        var query = QueryHelpers.ParseQuery(new Uri(Navigation.Uri).Query);
        if (query.TryGetValue("productId", out var idValue) && int.TryParse(idValue, out var id))
            _selected = _products.FirstOrDefault(p => p.Id == id);
        _selected ??= _products.FirstOrDefault();
    }

    private async Task OnPhotoSelectedAsync(InputFileChangeEventArgs e)
    {
        _error = null;
        _busy = true;
        try
        {
            // Downscale client-side: mobile photos are 8-12 MP; the server needs at most 2048 px.
            var resized = await e.File.RequestImageFileAsync("image/jpeg", 2048, 2048);
            await using var stream = resized.OpenReadStream(maxAllowedSize: 15 * 1024 * 1024);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            _photoBytes = ms.ToArray();
            _photoLoaded = true;
            StateHasChanged(); // render the stage div before JS init
            await InitStageAsync();
        }
        catch (Exception)
        {
            _error = "Моля, качете снимка във формат JPG или PNG до 10 MB.";
            _photoLoaded = false;
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task InitStageAsync()
    {
        _selfRef ??= DotNetObjectReference.Create(this);
        await JS.InvokeAsync<object>("nsiVisualizer.init", "viz-stage", _selfRef, (object?)null);
        _stageInitialized = true;
        var dataUrl = "data:image/jpeg;base64," + Convert.ToBase64String(_photoBytes!);
        await JS.InvokeAsync<object>("nsiVisualizer.loadPhotoFromDataUrl", dataUrl);
        await JS.InvokeVoidAsync("nsiVisualizer.setMode", "tap-add");
        await JS.InvokeVoidAsync("nsiVisualizer.setMaskVisible", true);
    }

    [JSInvokable]
    public async Task OnCanvasTapAsync(double x, double y, int label)
    {
        if (_busy || _photoBytes is null) return;
        _busy = true;
        _error = null;
        StateHasChanged();
        try
        {
            _points.Add(new SegmentPoint(x, y, label));
            SegmentResponse? result;
            string? error;

            if (_sessionToken is null)
            {
                (result, error) = await VisualizerService.SegmentAsync(_photoBytes, _points);
            }
            else
            {
                bool expired;
                (result, error, expired) = await VisualizerService.RefineAsync(_sessionToken, _points);
                if (expired)
                {
                    // Embedding cache expired — transparently re-upload the kept photo bytes.
                    (result, error) = await VisualizerService.SegmentAsync(_photoBytes, _points);
                }
            }

            if (result is null)
            {
                _points.RemoveAt(_points.Count - 1);
                _error = error ?? "Областта не можа да бъде разпозната автоматично. Можете да я маркирате ръчно с четката.";
                return;
            }

            _sessionToken = result.SessionToken;
            await JS.InvokeAsync<object>("nsiVisualizer.setMaskPng", result.MaskPng);

            if (!_hasMask)
            {
                _hasMask = true;
                var corners = await JS.InvokeAsync<double[]>("nsiVisualizer.defaultCornersFromMask");
                await JS.InvokeVoidAsync("nsiVisualizer.setCorners", (object)corners);
            }

            await ApplySelectedProductAsync();
        }
        finally
        {
            _busy = false;
            StateHasChanged();
        }
    }

    [JSInvokable]
    public Task OnMaskEditedAsync()
    {
        _hasMask = true;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task ApplySelectedProductAsync()
    {
        if (_selected is null || !_hasMask) return;
        await JS.InvokeAsync<object>("nsiVisualizer.setProductTexture",
            _selected.TexturePath, (double)_selected.TextureWidthMeters);
        await JS.InvokeVoidAsync("nsiVisualizer.render");
    }

    private async Task ResetAsync()
    {
        if (_stageInitialized)
            await JS.InvokeVoidAsync("nsiVisualizer.dispose");
        _stageInitialized = false;
        _photoLoaded = false;
        _photoBytes = null;
        _sessionToken = null;
        _points.Clear();
        _hasMask = false;
        _error = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_stageInitialized)
        {
            try { await JS.InvokeVoidAsync("nsiVisualizer.dispose"); }
            catch (JSDisconnectedException) { }
        }
        _selfRef?.Dispose();
    }
}
```

- [ ] **Step 2: Append the overlay style**

Append to the site stylesheet referenced by `index.html` (normally `src/NaturalStoneImpex.Client/wwwroot/css/app.css`):

```css
/* Product visualizer */
.viz-overlay {
    position: absolute;
    inset: 0;
    background: rgba(0, 0, 0, 0.45);
    z-index: 10;
}
```

- [ ] **Step 3: Build and verify the happy path manually**

```powershell
dotnet build
dotnet run --project src/NaturalStoneImpex.Api        # terminal 1 (models downloaded)
dotnet run --project src/NaturalStoneImpex.Client     # terminal 2
```

Prerequisite data: log into `/admin`, edit one product — upload a texture (any stone photo), set «Реална ширина» ≈ 1, enable it for the visualizer (admin UI fields arrive in Task 13 — until then set `IsVisualizerEnabled = 1` and `TextureImagePath` directly in the DB, or via Swagger `PUT /api/products/{id}` + `POST /api/products/{id}/texture`).

Open `https://localhost:5002/visualizer` and verify: consent gate works; photo uploads; tapping the ground shows the busy overlay then a green mask tint; the paved render appears over the tapped area. Expected errors also verifiable: without consent the file input is disabled; junk file → Bulgarian error.

- [ ] **Step 4: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): public page with upload and tap-to-segment flow"
```

---

