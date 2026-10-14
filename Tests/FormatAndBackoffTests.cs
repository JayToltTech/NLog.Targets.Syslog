using System.Text;
using NLog;
using NLog.Targets.Syslog;

namespace Tests;

public class FormatAndBackoffTests
{
    [Fact]
    public void Frame_IsOctetCountedRfc5424WithNilFieldsAndNoBom()
    {
        var logEvent = new LogEventInfo(LogLevel.Info, "logger", "ignored")
        {
            TimeStamp = new DateTime(2026, 10, 13, 12, 34, 56, DateTimeKind.Utc).AddTicks(1234567),
        };
        using var output = new MemoryStream();

        Rfc5424.AppendFrame(output, Facility.Local0, logEvent, "host", "App", "{\"é\":1}");

        var expectedMessage = "<134>1 2026-10-13T12:34:56.123456Z host App - - - {\"é\":1}";
        var expectedBytes = Encoding.UTF8.GetByteCount(expectedMessage);
        Assert.Equal($"{expectedBytes} {expectedMessage}", Encoding.UTF8.GetString(output.ToArray()));
    }

    [Theory]
    [InlineData("Fatal", 2)]
    [InlineData("Error", 3)]
    [InlineData("Warn", 4)]
    [InlineData("Info", 6)]
    [InlineData("Debug", 7)]
    [InlineData("Trace", 7)]
    public void Pri_CombinesFacilityAndSeverity(string level, int severity)
    {
        Assert.Equal((16 * 8) + severity, Rfc5424.Pri(Facility.Local0, LogLevel.FromString(level)));
    }

    [Theory]
    [InlineData(null, 48, "-")]
    [InlineData("", 48, "-")]
    [InlineData("my host\tname", 48, "my_host_name")]
    [InlineData("abcdef", 3, "abc")]
    public void HeaderField_IsPrintableAsciiTruncatedOrNil(string? value, int max, string expected)
    {
        Assert.Equal(expected, Rfc5424.HeaderField(value, max));
    }

    [Fact]
    public void Backoff_DoublesWithinJitterBandUpToCap()
    {
        var low = new Backoff(() => 0.0);
        var high = new Backoff(() => 1.0);
        var baseDelay = TimeSpan.FromSeconds(1);
        var cap = TimeSpan.FromSeconds(10);

        var lows = Enumerable.Range(0, 6).Select(_ => low.NextDelay(baseDelay, cap).TotalSeconds).ToArray();
        var highs = Enumerable.Range(0, 6).Select(_ => high.NextDelay(baseDelay, cap).TotalSeconds).ToArray();

        Assert.Equal([0.5, 1, 2, 4, 5, 5], lows);
        Assert.Equal([1, 2, 4, 8, 10, 10], highs);
    }

    [Fact]
    public void Backoff_ResetStartsOverAtBaseDelay()
    {
        var backoff = new Backoff(() => 1.0);
        for (var i = 0; i < 5; i++)
            backoff.NextDelay(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(2));

        backoff.Reset();

        Assert.Equal(TimeSpan.FromSeconds(1), backoff.NextDelay(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(2)));
    }

    [Fact]
    public void Backoff_SurvivesManyFailuresWithoutOverflow()
    {
        var backoff = new Backoff(() => 1.0);
        var last = TimeSpan.Zero;
        for (var i = 0; i < 1000; i++)
            last = backoff.NextDelay(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(2));

        Assert.Equal(TimeSpan.FromMinutes(2), last);
    }
}
