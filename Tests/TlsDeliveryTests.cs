using System.Net;
using System.Net.Sockets;
using NLog;
using NLog.Config;
using NLog.Targets.Syslog;

namespace Tests;

public sealed class TlsDeliveryTests : IDisposable
{
    private static readonly TimeSpan Receive = TimeSpan.FromSeconds(10);
    private readonly TestPki pki = new("Test CA");
    private readonly LogFactory factory = new();

    public void Dispose()
    {
        factory.Shutdown();
        factory.Dispose();
        pki.Dispose();
    }

    [Fact]
    public async Task DeliversOverMutualTls()
    {
        await using var server = new TestSyslogServer(pki.Server, pki.Ca);
        var logger = Start(Target(server.Port));

        logger.Info("hello");

        var frame = await server.NextFrameAsync(Receive);
        Assert.Matches(@"^<134>1 \S+Z test-host test-app - - - hello$", frame);
    }

    [Fact]
    public async Task RefusesServerFromAnotherCa()
    {
        using var rogue = new TestPki("Rogue CA");
        await using var server = new TestSyslogServer(rogue.Server, pki.Ca);
        var logger = Start(Target(server.Port));

        logger.Info("must not arrive");
        factory.Flush(TimeSpan.FromSeconds(5));

        await AssertNothingArrives(server);
        Assert.True(server.Connections > 0, "the target never reached the server");
    }

    [Fact]
    public async Task ServerRefusesClientWithoutCertificate()
    {
        await using var server = new TestSyslogServer(pki.Server, pki.Ca);
        var target = Target(server.Port);
        target.ClientCertificate = null;
        var logger = Start(target);

        logger.Info("must not arrive");
        factory.Flush(TimeSpan.FromSeconds(5));

        await AssertNothingArrives(server);
    }

    [Fact]
    public async Task ReconnectsAfterServerClosesConnection()
    {
        await using var server = new TestSyslogServer(pki.Server, pki.Ca, closeAfterFrames: 1);
        var logger = Start(Target(server.Port));

        logger.Info("first");
        Assert.EndsWith("first", await server.NextFrameAsync(Receive));
        await Task.Delay(500, TestContext.Current.CancellationToken);
        logger.Info("second");

        Assert.EndsWith("second", await server.NextFrameAsync(Receive));
        Assert.Equal(2, server.Connections);
    }

    [Fact]
    public async Task RecoversWhenServerComesUpAfterOutage()
    {
        var port = FreePort();
        var logger = Start(Target(port));

        logger.Info("during outage");
        await Task.Delay(1000, TestContext.Current.CancellationToken);
        await using var server = new TestSyslogServer(pki.Server, pki.Ca, port);
        logger.Info("after recovery");

        string frame;
        do
            frame = await server.NextFrameAsync(Receive);
        while (frame.EndsWith("during outage", StringComparison.Ordinal));
        Assert.EndsWith("after recovery", frame);
    }

    private SyslogTarget Target(int port) => new()
    {
        Server = "localhost",
        Port = port,
        Facility = Facility.Local0,
        Hostname = "test-host",
        AppName = "test-app",
        Layout = "${message}",
        ClientCertificate = pki.Client,
        TrustedCaCertificate = pki.Ca,
        RetryDelayMilliseconds = 100,
        MaxRetryDelaySeconds = 1,
        ConnectTimeoutSeconds = 2,
    };

    private Logger Start(SyslogTarget target)
    {
        var config = new LoggingConfiguration(factory);
        config.AddRuleForAllLevels(target);
        factory.Configuration = config;
        return factory.GetLogger("test");
    }

    private static async Task AssertNothingArrives(TestSyslogServer server)
    {
        await Task.Delay(1000, TestContext.Current.CancellationToken);
        Assert.False(server.Frames.Reader.TryRead(out var frame), $"unexpected frame: {frame}");
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
