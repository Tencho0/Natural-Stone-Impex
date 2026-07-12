using Microsoft.Extensions.Options;

namespace NaturalStoneImpex.Api.Services.Segmentation;

/// <summary>Singleton semaphore bounding concurrent CPU-heavy encoder runs.</summary>
public class EncodeGate
{
    public SemaphoreSlim Semaphore { get; }

    public EncodeGate(IOptions<VisualizerOptions> options)
    {
        Semaphore = new SemaphoreSlim(options.Value.MaxConcurrentEncodes);
    }
}
