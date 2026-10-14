using System.Globalization;
using System.Text;

namespace NLog.Targets.Syslog;

/// <summary>Builds octet-counted RFC 5424 frames (RFC 5425 section 4.3) with no structured data.</summary>
internal static class Rfc5424
{
    private const string Nil = "-";
    private const int MaxHostnameLength = 255;
    private const int MaxAppNameLength = 48;
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void AppendFrame(Stream output, Facility facility, LogEventInfo logEvent, string hostname, string appName, string message)
    {
        var header = string.Concat(
            "<", Pri(facility, logEvent.Level).ToString(CultureInfo.InvariantCulture), ">1 ",
            Timestamp(logEvent.TimeStamp), " ",
            HeaderField(hostname, MaxHostnameLength), " ",
            HeaderField(appName, MaxAppNameLength), " - - - ");
        var syslogMessage = Utf8NoBom.GetBytes(header + message);
        var octetCount = Encoding.ASCII.GetBytes(syslogMessage.Length.ToString(CultureInfo.InvariantCulture) + " ");
        output.Write(octetCount, 0, octetCount.Length);
        output.Write(syslogMessage, 0, syslogMessage.Length);
    }

    internal static int Pri(Facility facility, LogLevel level) => ((int)facility * 8) + Severity(level);

    private static int Severity(LogLevel level) => level.Ordinal switch
    {
        5 => 2, // Fatal -> Critical
        4 => 3, // Error -> Error
        3 => 4, // Warn -> Warning
        2 => 6, // Info -> Informational
        _ => 7, // Debug, Trace -> Debug
    };

    private static string Timestamp(DateTime timestamp) =>
        timestamp.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);

    internal static string HeaderField(string? value, int maxLength)
    {
        if (value is null || value.Length == 0)
            return Nil;
        var printable = new StringBuilder(Math.Min(value.Length, maxLength));
        foreach (var c in value)
        {
            if (printable.Length == maxLength)
                break;
            printable.Append(c is > ' ' and <= '~' ? c : '_');
        }
        return printable.ToString();
    }
}
