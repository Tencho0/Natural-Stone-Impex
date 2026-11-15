using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace NaturalStoneImpex.Api.Storefront;

/// <summary>
/// Double-submit guard for checkout (ruling R24). Each checkout form carries a one-time token from its GET.
/// The first POST with a token places the order; any other POST with that token, also a concurrent one, waits
/// for it and gets the same outcome, so a fast double-click (both POSTs carry the same full cart cookie)
/// creates one order. Only successes are kept, for <see cref="Lifetime"/>: a failed attempt can be retried
/// with the same token. A missing or malformed token is not guarded (a stale form still orders).
/// Its own cache: the shared IMemoryCache is size-limited for the visualizer's embeddings.
/// </summary>
public sealed class CheckoutGuard : IDisposable
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private readonly MemoryCache _outcomes = new(new MemoryCacheOptions { SizeLimit = 10_000 });
    private readonly Lock _lock = new();

    public static string NewToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));

    public static bool IsToken(string? token) => token is { Length: 32 } && token.All(char.IsAsciiHexDigitLower);

    /// <returns>The outcome, and whether it is another request's (a replay of the token).</returns>
    public async Task<(CheckoutOutcome Outcome, bool Replayed)> RunOnceAsync(string? token, Func<Task<CheckoutOutcome>> place)
    {
        if (!IsToken(token))
            return (await place(), false);

        Task<CheckoutOutcome>? first;
        var mine = new TaskCompletionSource<CheckoutOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_lock)
        {
            if (!_outcomes.TryGetValue(token!, out first))
                _outcomes.Set(token!, mine.Task, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime, Size = 1 });
        }
        if (first is not null)
            return (await first, true);

        try
        {
            var outcome = await place();
            if (!outcome.Success)
                Forget(token!, mine.Task);
            mine.SetResult(outcome);
            return (outcome, false);
        }
        catch (Exception ex)
        {
            Forget(token!, mine.Task);
            mine.SetException(ex);
            _ = mine.Task.Exception; // observed here; a waiting request rethrows it too
            throw;
        }
    }

    private void Forget(string token, Task<CheckoutOutcome> task)
    {
        lock (_lock)
        {
            if (_outcomes.TryGetValue(token, out Task<CheckoutOutcome>? stored) && stored == task)
                _outcomes.Remove(token);
        }
    }

    public void Dispose() => _outcomes.Dispose();
}
