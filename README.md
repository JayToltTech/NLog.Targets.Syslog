# NLog.Targets.Syslog (minimal)

An NLog target that sends RFC 5424 syslog over TLS (RFC 5425, octet-counted framing), with an optional client certificate for mutual TLS and an optional pinned CA. Targets `netstandard2.0` (.NET Framework 4.8 and older .NET) and `net10.0` (Windows, Linux, macOS).

This is a from-scratch replacement for the upstream [luigiberrettini/NLog.Targets.Syslog](https://github.com/luigiberrettini/NLog.Targets.Syslog), cut down to the one path in use. There is no UDP, RFC 3164, plain TCP, structured data or enforcement policies.

## Use

```csharp
var target = new SyslogTarget
{
    Server = "logs.example.com",
    Port = 6514,
    Facility = Facility.Local0,
    Hostname = "${machinename}",
    AppName = "MyApp",
    Layout = new JsonLayout { /* ... */ },
    ClientCertificate = clientCertWithPrivateKey,   // optional: mutual TLS
    TrustedCaCertificate = internalCa,              // optional: replaces the OS trust store
    QueueLimit = 5000,
};
```

`SyslogTarget` is an NLog `AsyncTaskTarget`, so it queues and batches on its own. Do not wrap it in an `AsyncTargetWrapper`.

## Delivery and retry

- Events queue in memory up to `QueueLimit` (default 10000). When the queue is full, the oldest events are discarded (`OverflowAction = Discard`).
- A batch that fails is dropped. The next attempt waits a capped exponential backoff with equal jitter: half of each step is fixed and half is random. It starts at `RetryDelayMilliseconds` (default 1 s) and doubles per consecutive failure up to `MaxRetryDelaySeconds` (default 120 s). Events logged meanwhile keep queueing, and one success resets the backoff.
- If a write on an already-open connection fails, the target reconnects at once and resends that batch a single time. A background read detects when the server closes the connection, so the next batch reconnects instead of writing into a dead socket.
- Shutdown cancels any wait in progress, so `LogManager.Shutdown()` never blocks on a backoff.

RFC 5425 has no application-level acknowledgement. A message written at the moment the server goes away can be lost.

## Test

```bash
dotnet run --project Tests/Tests.csproj -f net10.0
dotnet run --project Tests/Tests.csproj -f net8.0
```

The `net8.0` run exercises the library's `netstandard2.0` build, and rolls forward onto whatever runtime is installed. The tests start a loopback mTLS syslog server with a throwaway CA.
