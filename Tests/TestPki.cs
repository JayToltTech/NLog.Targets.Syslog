using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Tests;

/// <summary>Throwaway CA plus server and client leaves, generated per test run.</summary>
internal sealed class TestPki : IDisposable
{
    private static readonly Oid ServerAuth = new("1.3.6.1.5.5.7.3.1");
    private static readonly Oid ClientAuth = new("1.3.6.1.5.5.7.3.2");

    public TestPki(string name)
    {
        Ca = CreateCa(name);
        Server = Issue(Ca, "CN=localhost", ServerAuth, withLocalhostSan: true);
        Client = Issue(Ca, "CN=test-client", ClientAuth, withLocalhostSan: false);
    }

    public X509Certificate2 Ca { get; }

    public X509Certificate2 Server { get; }

    public X509Certificate2 Client { get; }

    public void Dispose()
    {
        Ca.Dispose();
        Server.Dispose();
        Client.Dispose();
    }

    private static X509Certificate2 CreateCa(string name)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        using var ca = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        return Reload(ca);
    }

    private static X509Certificate2 Issue(X509Certificate2 ca, string subject, Oid usage, bool withLocalhostSan)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([usage], false));
        if (withLocalhostSan)
        {
            var san = new SubjectAlternativeNameBuilder();
            san.AddDnsName("localhost");
            request.CertificateExtensions.Add(san.Build());
        }
        var serial = RandomNumberGenerator.GetBytes(16);
        using var issued = request.Create(ca, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1), serial);
        using var withKey = issued.CopyWithPrivateKey(key);
        return Reload(withKey);
    }

    // SslStream on macOS needs the key in a PKCS#12 import, not the ephemeral key CertificateRequest made.
    private static X509Certificate2 Reload(X509Certificate2 certificate)
    {
        var pkcs12 = certificate.Export(X509ContentType.Pkcs12);
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12(pkcs12, null);
#else
        return new X509Certificate2(pkcs12);
#endif
    }
}
