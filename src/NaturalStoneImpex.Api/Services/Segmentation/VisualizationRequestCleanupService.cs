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
