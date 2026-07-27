### Task 14: Retention job, documentation, and E2E checklist

**Files:**
- Create: `src/NaturalStoneImpex.Api/Services/Segmentation/VisualizationRequestCleanupService.cs`
- Modify: `src/NaturalStoneImpex.Api/Program.cs`
- Modify: `docs/api-endpoints.md`
- Modify: `docs/database-schema.md`
- Modify: `CLAUDE.md` (commands section)

- [ ] **Step 1: Quota-row retention job**

Create `src/NaturalStoneImpex.Api/Services/Segmentation/VisualizationRequestCleanupService.cs` (spec §7.2: prune rows older than 90 days — they hold no personal data, this is just hygiene):

```csharp
using Microsoft.EntityFrameworkCore;
using NaturalStoneImpex.Api.Data;

namespace NaturalStoneImpex.Api.Services.Segmentation;

public class VisualizationRequestCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VisualizationRequestCleanupService> _logger;

    public VisualizationRequestCleanupService(IServiceScopeFactory scopeFactory,
        ILogger<VisualizationRequestCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cutoff = DateTime.UtcNow.AddDays(-90);
                var removed = await db.VisualizationRequests
                    .Where(r => r.CreatedAt < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);
                if (removed > 0)
                    _logger.LogInformation("Pruned {Count} visualization request rows", removed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Visualization request cleanup failed");
            }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
```

Register in `src/NaturalStoneImpex.Api/Program.cs` next to the other visualizer registrations:

```csharp
builder.Services.AddHostedService<VisualizationRequestCleanupService>();
```

Run `dotnet build` + `dotnet test` — all green.

- [ ] **Step 2: Update the API contract doc**

In `docs/api-endpoints.md`, add a `### Visualizer` section following the document's existing table style:

```markdown
### Visualizer (Визуализатор)

| Method | Endpoint                                | Description                                        | Auth  |
|--------|-----------------------------------------|----------------------------------------------------|-------|
| GET    | /api/visualizer/products                | Visualizer-enabled products with texture info      | No    |
| POST   | /api/visualizer/segment                 | Segment uploaded photo (multipart: photo + points) | No    |
| POST   | /api/visualizer/segment/{sessionToken}  | Refine mask with additional points (JSON body)     | No    |
| POST   | /api/products/{id}/texture              | Upload product texture image                       | Admin |

`POST /api/visualizer/segment` responses: `200 { sessionToken, maskPng, width, height }`,
`400` invalid photo/points, `429` daily quota reached, `503` visualizer disabled or busy.
`POST /api/visualizer/segment/{sessionToken}` additionally returns `404` when the server-side
embedding cache has expired (client re-uploads the photo). Photos are processed in memory and
never stored; quotas are enforced per hashed IP per day (see `Visualizer` section in appsettings).
```

- [ ] **Step 3: Update the database schema doc**

In `docs/database-schema.md`, add the three `Product` columns (`IsVisualizerEnabled bit NOT NULL DEFAULT 0`, `TextureImagePath nvarchar(500) NULL`, `TextureWidthMeters decimal(18,2) NOT NULL DEFAULT 1.00`) to the Product table definition, and append a `VisualizationRequests` table section (`Id int PK`, `IpHash nvarchar(64)`, `Status int`, `DurationMs int`, `CreatedAt datetime2`, indexes on `(IpHash, CreatedAt)` and `CreatedAt`), following the document's existing format.

- [ ] **Step 4: Document the model download in CLAUDE.md**

In `CLAUDE.md`'s Commands section add:

```bash
# Download visualizer ONNX models (one-time, required for the visualizer feature)
powershell -File scripts/download-visualizer-models.ps1
```

- [ ] **Step 5: Full E2E checklist (manual)**

Run API + client with models downloaded and at least two visualizer-enabled products, then walk through:

1. `/visualizer` from nav — consent gate blocks upload until checked.
2. Upload a real outdoor photo (from a phone if possible) — tap the driveway → mask appears ≤ 6 s.
3. «Премахни» tap on an over-segmented region shrinks the mask; brush/eraser fine-tune it.
4. «Перспектива» — drag corners; stones follow; scale/rotation sliders work.
5. Switch products repeatedly — updates feel instant (< 0.5 s), no network calls in DevTools.
6. Compare slider, «Изтегли» (valid JPEG), «Добави в количката» (badge updates), «Виж продукта».
7. Wait 16+ minutes (or set `EmbeddingCacheMinutes: 0` temporarily), tap again — flow recovers transparently via re-upload.
8. Set `PerIpDailyLimit: 1` temporarily — second photo upload shows the Bulgarian quota message.
9. Set `Enabled: false` — page shows service-unavailable behavior (tap → error alert; feature degrades to brush-only marking).
10. DevTools device emulation (phone): camera capture input, bottom layout, touch brush and handles usable.
11. Chrome with `--disable-webgl` (or harness `?fallback=1`): canvas-2D fallback renders.
12. Verify nothing was written to `wwwroot/uploads` during customer flows and `VisualizationRequests` has one row per uploaded photo.

Record any failures as issues; do not ship with a failing checklist item.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat(visualizer): retention job, docs and E2E checklist"
```

---

## Post-plan notes for the implementer

- **Model contract risk (highest-risk item, surfaces in Task 3):** ONNX exports vary in tensor names/shapes. The wrapper reads input names dynamically and feeds only declared inputs, and the integration test pins behavior. If the chosen export deviates beyond that flexibility, fix the wrapper (not the callers) — `ISamModel` is the stable boundary.
- **Perspective defaults (`GROUND_H = 15`, top edge 45%)** are heuristics tuned for typical slightly-downward yard photos. If early testing shows stones too stretched/compressed near the top, tune `GROUND_H` first; the user-facing handles compensate for individual photos.
- **Spec traceability:** spec §3 flows → Tasks 10–12; §5.1 → Tasks 3–6; §5.2–5.3 → Task 7; §5.4 ladder → Tasks 8, 10 (brush fallback on 503, canvas-2D on no-WebGL); §7 → Tasks 1, 5; §8 → Task 13; §9 quotas/latency → Tasks 5–6; §10 privacy (in-memory only, no third parties) → Tasks 5–6; §7.2 pruning + docs → Task 14.



