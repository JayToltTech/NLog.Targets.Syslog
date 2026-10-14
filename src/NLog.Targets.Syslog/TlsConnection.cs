using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
#if !NET
using System.Runtime.InteropServices;
#endif

namespace NLog.Targets.Syslog;

/// <summary>One TLS session to the syslog server; a background read notices when the server goes away.</summary>
internal sealed class TlsConnection : IDisposable
{
    private const int KeepAliveIdleSeconds = 30;
    private const int KeepAliveIntervalSeconds = 5;
#if NET
    private const int KeepAliveProbes = 3;
#endif

    private readonly TcpClient tcp;
    private readonly SslStream ssl;
    private volatile bool closed;

    private TlsConnection(TcpClient tcp, SslStream ssl)
    {
        this.tcp = tcp;
        this.ssl = ssl;
        _ = WatchForCloseAsync();
    }

    public bool IsOpen => !closed;

    public static async Task<TlsConnection> OpenAsync(TlsEndpoint endpoint, CancellationToken token)
    {
        var tcp = new TcpClient();
        try
        {
            ConfigureKeepAlive(tcp.Client);
            await ConnectAsync(tcp, endpoint, token).ConfigureAwait(false);
            var ssl = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false, endpoint.ValidateServerCertificate);
            await AuthenticateAsync(ssl, endpoint, token).ConfigureAwait(false);
            return new TlsConnection(tcp, ssl);
        }
        catch
        {
            tcp.Dispose();
            throw;
        }
    }

    public async Task WriteAsync(MemoryStream payload, CancellationToken token)
    {
        if (closed)
            throw new IOException("Syslog server closed the connection");
        payload.Position = 0;
        await payload.CopyToAsync(ssl, 81920, token).ConfigureAwait(false);
        await ssl.FlushAsync(token).ConfigureAwait(false);
    }

    public void Dispose()
    {
        closed = true;
        ssl.Dispose();
        tcp.Dispose();
    }

    private async Task WatchForCloseAsync()
    {
        try
        {
            // Syslog servers send no application data: drain anything (TLS 1.3 tickets) until EOF or reset.
            await ssl.CopyToAsync(Stream.Null).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or SocketException)
        {
            Common.InternalLogger.Debug(exception, "[Syslog] Connection read ended");
        }
        closed = true;
    }

    private static async Task ConnectAsync(TcpClient tcp, TlsEndpoint endpoint, CancellationToken token)
    {
#if NET
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(endpoint.ConnectTimeout);
        await tcp.ConnectAsync(endpoint.Server, endpoint.Port, timeout.Token).ConfigureAwait(false);
#else
        var connect = tcp.ConnectAsync(endpoint.Server, endpoint.Port);
        var finished = await Task.WhenAny(connect, Task.Delay(endpoint.ConnectTimeout, token)).ConfigureAwait(false);
        if (finished != connect)
        {
            _ = connect.ContinueWith(t => t.Exception, TaskScheduler.Default);
            token.ThrowIfCancellationRequested();
            throw new TimeoutException($"Connecting to {endpoint.Server}:{endpoint.Port} timed out");
        }
        await connect.ConfigureAwait(false);
#endif
    }

    private static Task AuthenticateAsync(SslStream ssl, TlsEndpoint endpoint, CancellationToken token)
    {
        var clientCertificates = endpoint.ClientCertificate is null
            ? null
            : new X509CertificateCollection { endpoint.ClientCertificate };
#if NET
        var options = new SslClientAuthenticationOptions
        {
            TargetHost = endpoint.Server,
            ClientCertificates = clientCertificates,
            EnabledSslProtocols = SslProtocols.None,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
        };
        return ssl.AuthenticateAsClientAsync(options, token);
#else
        token.ThrowIfCancellationRequested();
        return ssl.AuthenticateAsClientAsync(endpoint.Server, clientCertificates, SslProtocols.None, checkCertificateRevocation: false);
#endif
    }

    private static void ConfigureKeepAlive(Socket socket)
    {
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
#if NET
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, KeepAliveIdleSeconds);
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, KeepAliveIntervalSeconds);
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, KeepAliveProbes);
#else
        // netstandard2.0 exposes no portable tuning; Windows (the .NET Framework host) takes SIO_KEEPALIVE_VALS.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            _ = socket.IOControl(IOControlCode.KeepAliveValues, WindowsKeepAliveValues(), null);
#endif
    }

#if !NET
    private static byte[] WindowsKeepAliveValues()
    {
        var values = new byte[12];
        BitConverter.GetBytes(1u).CopyTo(values, 0);
        BitConverter.GetBytes((uint)KeepAliveIdleSeconds * 1000).CopyTo(values, 4);
        BitConverter.GetBytes((uint)KeepAliveIntervalSeconds * 1000).CopyTo(values, 8);
        return values;
    }
#endif
}
