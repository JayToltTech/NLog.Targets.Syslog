using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace NLog.Targets.Syslog;

/// <summary>Where to connect and how to authenticate both ends.</summary>
internal sealed class TlsEndpoint
{
    public TlsEndpoint(string server, int port, X509Certificate2? clientCertificate, X509Certificate2? trustedCa, TimeSpan connectTimeout)
    {
        Server = server;
        Port = port;
        ClientCertificate = clientCertificate;
        TrustedCa = trustedCa;
        ConnectTimeout = connectTimeout;
    }

    public string Server { get; }

    public int Port { get; }

    public X509Certificate2? ClientCertificate { get; }

    /// <summary>When set, the server chain must end at this CA and the OS trust store is ignored.</summary>
    public X509Certificate2? TrustedCa { get; }

    public TimeSpan ConnectTimeout { get; }

    public bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (TrustedCa is null)
            return errors == SslPolicyErrors.None;
        if (certificate is null || (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != SslPolicyErrors.None)
            return false;
        using var leaf = new X509Certificate2(certificate);
        return ChainsToTrustedCa(leaf, TrustedCa);
    }

    internal static bool ChainsToTrustedCa(X509Certificate2 leaf, X509Certificate2 trustedCa)
    {
        using var pinned = new X509Chain();
        pinned.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
#if NET
        pinned.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        pinned.ChainPolicy.CustomTrustStore.Add(trustedCa);
        return pinned.Build(leaf);
#else
        pinned.ChainPolicy.ExtraStore.Add(trustedCa);
        pinned.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        if (!pinned.Build(leaf))
            return false;
        var root = pinned.ChainElements[pinned.ChainElements.Count - 1].Certificate;
        return root.RawData.SequenceEqual(trustedCa.RawData);
#endif
    }
}
