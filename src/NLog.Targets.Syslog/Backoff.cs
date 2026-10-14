using System.Diagnostics.CodeAnalysis;

namespace NLog.Targets.Syslog;

/// <summary>Capped exponential backoff with equal jitter: half the step is fixed, half is random.</summary>
internal sealed class Backoff
{
    private const int MaxExponent = 30;
    private readonly Func<double> nextUnitRandom;
    private int consecutiveFailures;

    public Backoff(Func<double>? nextUnitRandom = null)
    {
        this.nextUnitRandom = nextUnitRandom ?? NewJitterSource();
    }

    public int ConsecutiveFailures => Volatile.Read(ref consecutiveFailures);

    public TimeSpan NextDelay(TimeSpan baseDelay, TimeSpan maxDelay)
    {
        var exponent = Math.Min(Interlocked.Increment(ref consecutiveFailures) - 1, MaxExponent);
        var step = Math.Min(maxDelay.TotalMilliseconds, baseDelay.TotalMilliseconds * Math.Pow(2, exponent));
        return TimeSpan.FromMilliseconds((step / 2) + (nextUnitRandom() * step / 2));
    }

    public void Reset() => Interlocked.Exchange(ref consecutiveFailures, 0);

    [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Retry jitter only spreads reconnects; it protects nothing.")]
    [SuppressMessage("Security", "S2245:Using pseudorandom number generators (PRNGs) is security-sensitive", Justification = "Retry jitter only spreads reconnects; it protects nothing.")]
    private static Func<double> NewJitterSource()
    {
#if NET
        return Random.Shared.NextDouble;
#else
        var random = new Random();
        return () =>
        {
            lock (random)
                return random.NextDouble();
        };
#endif
    }
}
