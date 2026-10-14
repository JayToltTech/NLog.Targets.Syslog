namespace NLog.Targets.Syslog;

/// <summary>Syslog facility codes from RFC 5424 section 6.2.1.</summary>
public enum Facility
{
    /// <summary>Kernel messages.</summary>
    Kernel = 0,
    /// <summary>User-level messages.</summary>
    User = 1,
    /// <summary>Mail system.</summary>
    Mail = 2,
    /// <summary>System daemons.</summary>
    Daemons = 3,
    /// <summary>Security and authorization messages.</summary>
    Authorization = 4,
    /// <summary>Messages generated internally by syslogd.</summary>
    Syslog = 5,
    /// <summary>Line printer subsystem.</summary>
    Printer = 6,
    /// <summary>Network news subsystem.</summary>
    News = 7,
    /// <summary>UUCP subsystem.</summary>
    Uucp = 8,
    /// <summary>Clock daemon.</summary>
    Clock = 9,
    /// <summary>Security and authorization messages (private).</summary>
    Authorization2 = 10,
    /// <summary>FTP daemon.</summary>
    Ftp = 11,
    /// <summary>NTP subsystem.</summary>
    Ntp = 12,
    /// <summary>Log audit.</summary>
    Audit = 13,
    /// <summary>Log alert.</summary>
    Alert = 14,
    /// <summary>Clock daemon (secondary).</summary>
    Clock2 = 15,
    /// <summary>Local use 0.</summary>
    Local0 = 16,
    /// <summary>Local use 1.</summary>
    Local1 = 17,
    /// <summary>Local use 2.</summary>
    Local2 = 18,
    /// <summary>Local use 3.</summary>
    Local3 = 19,
    /// <summary>Local use 4.</summary>
    Local4 = 20,
    /// <summary>Local use 5.</summary>
    Local5 = 21,
    /// <summary>Local use 6.</summary>
    Local6 = 22,
    /// <summary>Local use 7.</summary>
    Local7 = 23,
}
