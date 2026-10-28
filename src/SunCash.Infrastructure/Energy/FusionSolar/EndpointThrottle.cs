using System.Collections.Concurrent;

namespace SunCash.Infrastructure.Energy.FusionSolar;

/// <summary>
/// Rispetta un intervallo minimo fra due chiamate allo stesso endpoint, aspettando il proprio turno.
/// Singleton: lo stato deve sopravvivere fra un'esecuzione del job e l'altra.
/// </summary>
public sealed class EndpointThrottle(TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, (SemaphoreSlim Gate, DateTimeOffset Last)> _endpoints = new();

    /// <summary>Aspetta che l'endpoint sia di nuovo chiamabile, poi registra la chiamata.</summary>
    public async Task WaitTurnAsync(string endpoint, TimeSpan minInterval, CancellationToken ct)
    {
        var gate = _endpoints.GetOrAdd(endpoint, _ => (new SemaphoreSlim(1, 1), DateTimeOffset.MinValue)).Gate;
        await gate.WaitAsync(ct);
        try
        {
            var last = _endpoints[endpoint].Last;
            if (last != DateTimeOffset.MinValue)
            {
                var wait = last + minInterval - clock.GetUtcNow();
                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait, clock, ct);
            }

            _endpoints[endpoint] = (gate, clock.GetUtcNow());
        }
        finally
        {
            gate.Release();
        }
    }
}
