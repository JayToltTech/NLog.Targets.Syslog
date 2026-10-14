using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Channels;

namespace Tests;

/// <summary>Loopback RFC 5425 receiver that requires a client certificate issued by the given CA.</summary>
internal sealed class TestSyslogServer : IAsyncDisposable
{
    private readonly TcpListener listener;
    private readonly X509Certificate2 serverCertificate;
    private readonly X509Certificate2 clientCa;
    private readonly int closeAfterFrames;
    private readonly CancellationTokenSource stop = new();
    private readonly Task acceptLoop;

    public TestSyslogServer(X509Certificate2 serverCertificate, X509Certificate2 clientCa, int port = 0, int closeAfterFrames = int.MaxValue)
    {
        this.serverCertificate = serverCertificate;
        this.clientCa = clientCa;
        this.closeAfterFrames = closeAfterFrames;
        listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        acceptLoop = AcceptLoopAsync();
    }

    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    public Channel<string> Frames { get; } = Channel.CreateUnbounded<string>();

    public int Connections { get; private set; }

    public async Task<string> NextFrameAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        return await Frames.Reader.ReadAsync(cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        listener.Stop();
        try
        {
#pragma warning disable VSTHRD003 // This instance started the loop.
            await acceptLoop;
#pragma warning restore VSTHRD003
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or SocketException)
        {
            // Stopping the listener ends the loop with one of these.
        }
        stop.Dispose();
    }

    private async Task AcceptLoopAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(stop.Token);
            Connections++;
            _ = ServeAsync(client);
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        await using (var ssl = new SslStream(client.GetStream(), false))
        {
            try
            {
                await ssl.AuthenticateAsServerAsync(ServerOptions(), stop.Token);
                await ReadFramesAsync(ssl);
            }
            catch (Exception exception) when (exception is IOException or System.Security.Authentication.AuthenticationException or OperationCanceledException)
            {
                // A refused handshake or a closed connection ends this session only.
            }
        }
    }

    private SslServerAuthenticationOptions ServerOptions() => new()
    {
        ServerCertificate = serverCertificate,
        ClientCertificateRequired = true,
        CertificateChainPolicy = new X509ChainPolicy
        {
            TrustMode = X509ChainTrustMode.CustomRootTrust,
            CustomTrustStore = { clientCa },
            RevocationMode = X509RevocationMode.NoCheck,
        },
        RemoteCertificateValidationCallback = (_, certificate, _, errors) => certificate is not null && errors == SslPolicyErrors.None,
    };

    private async Task ReadFramesAsync(SslStream ssl)
    {
        for (var received = 0; received < closeAfterFrames; received++)
        {
            var length = await ReadOctetCountAsync(ssl);
            if (length < 0)
                return;
            var frame = new byte[length];
            await ssl.ReadExactlyAsync(frame, stop.Token);
            await Frames.Writer.WriteAsync(Encoding.UTF8.GetString(frame), stop.Token);
        }
    }

    private async Task<int> ReadOctetCountAsync(SslStream ssl)
    {
        var digits = new StringBuilder();
        var one = new byte[1];
        while (await ssl.ReadAsync(one, stop.Token) == 1)
        {
            if (one[0] == (byte)' ')
                return int.Parse(digits.ToString(), CultureInfo.InvariantCulture);
            digits.Append((char)one[0]);
        }
        return -1;
    }
}
