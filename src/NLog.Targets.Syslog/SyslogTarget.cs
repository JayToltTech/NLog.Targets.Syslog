using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using NLog.Common;
using NLog.Layouts;

namespace NLog.Targets.Syslog;

/// <summary>Sends RFC 5424 syslog over TLS (RFC 5425), optionally with a client certificate and a pinned CA.</summary>
/// <remarks>
/// Queueing, batching and overflow come from <see cref="AsyncTaskTarget"/>. A failed batch is dropped and the
/// next write waits a capped, jittered exponential backoff while new events keep queueing.
/// </remarks>
[Target("Syslog")]
public sealed class SyslogTarget : AsyncTaskTarget
{
    private readonly Backoff backoff = new();
    private TlsConnection? connection;

    /// <summary>Builds the target with a batch size and a 1 s first retry delay suited to a remote TCP sink.</summary>
    public SyslogTarget()
    {
        BatchSize = 100;
        RetryDelayMilliseconds = 1000;
    }

    /// <summary>Syslog server host name; also the name its certificate must match.</summary>
    public string Server { get; set; } = "localhost";

    /// <summary>Syslog server port. Default 6514, the RFC 5425 port.</summary>
    public int Port { get; set; } = 6514;

    /// <summary>Syslog facility. Default <see cref="Facility.User"/>.</summary>
    public Facility Facility { get; set; } = Facility.User;

    /// <summary>RFC 5424 HOSTNAME.</summary>
    public Layout Hostname { get; set; } = "${machinename}";

    /// <summary>RFC 5424 APP-NAME.</summary>
    public Layout AppName { get; set; } = "${processname}";

    /// <summary>Client certificate with private key for mutual TLS, or null for none.</summary>
    public X509Certificate2? ClientCertificate { get; set; }

    /// <summary>CA the server certificate must chain to, replacing the OS trust store; null uses the OS store.</summary>
    public X509Certificate2? TrustedCaCertificate { get; set; }

    /// <summary>Upper bound on the delay between reconnect attempts.</summary>
    public int MaxRetryDelaySeconds { get; set; } = 120;

    /// <summary>Time allowed for the TCP connect before the attempt counts as a failure.</summary>
    public int ConnectTimeoutSeconds { get; set; } = 10;

    /// <inheritdoc/>
    protected override Task WriteAsyncTask(LogEventInfo logEvent, CancellationToken cancellationToken) =>
        WriteAsyncTask([logEvent], cancellationToken);

    /// <inheritdoc/>
    protected override async Task WriteAsyncTask(IList<LogEventInfo> logEvents, CancellationToken cancellationToken)
    {
        using var payload = new MemoryStream();
        foreach (var logEvent in logEvents)
            AppendFrame(payload, logEvent);

        var reused = connection is { IsOpen: true };
        try
        {
            await SendAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (reused && IsConnectionFailure(exception))
        {
            InternalLogger.Debug(exception, "[Syslog] Reused connection failed; reconnecting once");
            await SendAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        backoff.Reset();
    }

    /// <inheritdoc/>
    protected override bool RetryFailedAsyncTask(Exception exception, CancellationToken cancellationToken, int retryCountRemaining, out TimeSpan retryDelay)
    {
        retryDelay = backoff.NextDelay(TimeSpan.FromMilliseconds(RetryDelayMilliseconds), TimeSpan.FromSeconds(MaxRetryDelaySeconds));
        InternalLogger.Warn(exception, "[Syslog] Send to {0}:{1} failed ({2} in a row); next attempt in {3:F1} s", Server, Port, backoff.ConsecutiveFailures, retryDelay.TotalSeconds);
        return !cancellationToken.IsCancellationRequested && retryCountRemaining >= 0;
    }

    /// <inheritdoc/>
    protected override void CloseTarget()
    {
        base.CloseTarget();
        DropConnection();
    }

    private void AppendFrame(MemoryStream payload, LogEventInfo logEvent)
    {
        Rfc5424.AppendFrame(payload, Facility, logEvent,
            RenderLogEvent(Hostname, logEvent), RenderLogEvent(AppName, logEvent), RenderLogEvent(Layout, logEvent));
    }

    private async Task SendAsync(MemoryStream payload, CancellationToken cancellationToken)
    {
        try
        {
            if (connection is not { IsOpen: true })
            {
                DropConnection();
                connection = await TlsConnection.OpenAsync(Endpoint(), cancellationToken).ConfigureAwait(false);
            }
            await connection.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            DropConnection();
            throw;
        }
    }

    private TlsEndpoint Endpoint() =>
        new(Server, Port, ClientCertificate, TrustedCaCertificate, TimeSpan.FromSeconds(ConnectTimeoutSeconds));

    private void DropConnection()
    {
        connection?.Dispose();
        connection = null;
    }

    private static bool IsConnectionFailure(Exception exception) =>
        exception is IOException or SocketException or ObjectDisposedException;
}
