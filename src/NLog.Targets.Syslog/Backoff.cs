using System.Security.Cryptography;

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

    // A CSPRNG costs nothing at reconnect rates and keeps every scanner quiet without a suppression.
    private static Func<double> NewJitterSource() => () =>
    {
        var bytes = new byte[4];
#if NET
        RandomNumberGenerator.Fill(bytes);
#else
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(bytes);
#endif
        return BitConverter.ToUInt32(bytes, 0) / (uint.MaxValue + 1.0);
    };
}
