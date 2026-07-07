// Licensed under the BSD license
// See the LICENSE file in the project root for more information

using System;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace NLog.Targets.Syslog.Settings
{
    /// <inheritdoc />
    /// <summary>TLS configuration</summary>
    public class TlsConfig : NotifyPropertyChanged
    {
        private bool enabled;
        private bool useClientCertificates;
        private StoreLocation certificateStoreLocation;
        private StoreName certificateStoreName;
        private X509FindType certificateFilterType;
        private string certificateFilterValue;
        private X509Certificate2 clientCertificate;
        private string pinnedCaCertificatePath;
        private X509Certificate2 pinnedCaCertificate;

        /// <summary>Whether to use TLS or not (TLS 1.2 only)</summary>
        public bool Enabled
        {
            get => enabled;
            set => SetProperty(ref enabled, value);
        }

        /// <summary>Whether to use client certificates or not</summary>
        public bool UseClientCertificates
        {
            get => useClientCertificates;
            set => SetProperty(ref useClientCertificates, value);
        }

        /// <summary>The X.509 certificate store location</summary>
        public StoreLocation CertificateStoreLocation
        {
            get => certificateStoreLocation;
            set => SetProperty(ref certificateStoreLocation, value);
        }

        /// <summary>The X.509 certificate store name</summary>
        public StoreName CertificateStoreName
        {
            get => certificateStoreName;
            set => SetProperty(ref certificateStoreName, value);
        }

        /// <summary>The type of filter to apply to the certificate collection</summary>
        public X509FindType CertificateFilterType
        {
            get => certificateFilterType;
            set => SetProperty(ref certificateFilterType, value);
        }

        /// <summary>The value against which to filter the certificate collection</summary>
        /// <remarks> If omitted the certificate collection is not filtered</remarks>
        public string CertificateFilterValue
        {
            get => certificateFilterValue;
            set => SetProperty(ref certificateFilterValue, value);
        }

        /// <summary>A client certificate (with private key) supplied directly, bypassing the certificate store</summary>
        /// <remarks>
        /// When set (and <see cref="UseClientCertificates" /> is true) this certificate is presented for
        /// mTLS instead of a <see cref="CertificateStoreLocation" />/<see cref="CertificateFilterValue" />
        /// store lookup. Intended for certificates embedded in the application rather than installed in the
        /// OS store. Cannot be expressed in XML config; set it programmatically.
        /// </remarks>
        public X509Certificate2 ClientCertificate
        {
            get => clientCertificate;
            set => SetProperty(ref clientCertificate, value);
        }

        /// <summary>Path to a CA certificate (PEM or DER) the server certificate chain must terminate at</summary>
        /// <remarks>
        /// When set, the server certificate is validated by pinning: the presented chain must build to
        /// this exact CA, independent of the operating system trust store. When omitted the default
        /// platform validation (OS trust store) is used.
        /// </remarks>
        public string PinnedCaCertificatePath
        {
            get => pinnedCaCertificatePath;
            set => SetProperty(ref pinnedCaCertificatePath, value);
        }

        /// <summary>A CA certificate the server certificate chain must terminate at, supplied directly</summary>
        /// <remarks>
        /// Takes precedence over <see cref="PinnedCaCertificatePath" />. Intended for a CA certificate
        /// embedded in the application rather than deployed as a file. Cannot be expressed in XML config;
        /// set it programmatically.
        /// </remarks>
        public X509Certificate2 PinnedCaCertificate
        {
            get => pinnedCaCertificate;
            set => SetProperty(ref pinnedCaCertificate, value);
        }

        /// <summary>Builds a new instance of the TlsConfig class</summary>
        public TlsConfig()
        {
            enabled = false;
            useClientCertificates = false;
            certificateStoreLocation = StoreLocation.CurrentUser;
            certificateStoreName = StoreName.My;
            certificateFilterType = X509FindType.FindBySubjectName;
            certificateFilterValue = null;
            clientCertificate = null;
            pinnedCaCertificatePath = null;
            pinnedCaCertificate = null;
        }

        internal X509Certificate2Collection RetrieveClientCertificates()
        {
            if (!useClientCertificates)
                return null;

            // A directly-supplied certificate (e.g. embedded in the application) bypasses the store.
            if (clientCertificate != null)
                return new X509Certificate2Collection(clientCertificate);

            var store = new X509Store(certificateStoreName, certificateStoreLocation);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                return certificateFilterValue == null ? store.Certificates : store.Certificates.Find(certificateFilterType, BuildFindValue(), false);
            }
            finally
            {
                store.Close();
            }
        }

        /// <summary>
        /// Builds a server certificate validation callback that pins the chain to
        /// <see cref="PinnedCaCertificatePath" />, or null to use default platform validation.
        /// </summary>
        internal RemoteCertificateValidationCallback BuildServerCertificateValidationCallback()
        {
            // A directly-supplied CA (e.g. embedded in the application) takes precedence over a path.
            // Loaded once, at configuration time, so a bad path fails fast and closed.
            var pinnedCa = pinnedCaCertificate ??
                (string.IsNullOrEmpty(pinnedCaCertificatePath) ? null : new X509Certificate2(pinnedCaCertificatePath));
            if (pinnedCa == null)
                return null;

            return (sender, certificate, chain, sslPolicyErrors) =>
            {
                // Hostname mismatch or a missing server certificate are never acceptable,
                // regardless of which CA signed the chain.
                if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
                    return false;
                if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0)
                    return false;
                if (certificate == null)
                    return false;

                using (var pinnedChain = new X509Chain())
                using (var serverCert = new X509Certificate2(certificate))
                {
                    // Match AuthenticateAsClient(checkCertificateRevocation: false).
                    pinnedChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    // The internal CA is intentionally NOT in the OS trust store; supply it
                    // via ExtraStore and tolerate the resulting "untrusted root" status, which
                    // we replace with an explicit pin check below.
                    pinnedChain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                    pinnedChain.ChainPolicy.ExtraStore.Add(pinnedCa);

                    pinnedChain.Build(serverCert);

                    // Any status other than a clean build or the expected untrusted-root
                    // (time-invalid, revoked, partial chain, ...) rejects the connection.
                    var statusAcceptable = pinnedChain.ChainStatus.All(s =>
                        s.Status == X509ChainStatusFlags.NoError ||
                        s.Status == X509ChainStatusFlags.UntrustedRoot);
                    if (!statusAcceptable)
                        return false;

                    // The chain must actually terminate at the pinned CA.
                    var elements = pinnedChain.ChainElements;
                    if (elements.Count == 0)
                        return false;
                    var root = elements[elements.Count - 1].Certificate;
                    return string.Equals(root.Thumbprint, pinnedCa.Thumbprint, StringComparison.OrdinalIgnoreCase);
                }
            };
        }

        private object BuildFindValue()
        {
            switch (certificateFilterType)
            {
                case X509FindType.FindByTimeExpired:
                case X509FindType.FindByTimeNotYetValid:
                case X509FindType.FindByTimeValid:
                {
                    return DateTime.Parse(certificateFilterValue, System.Globalization.CultureInfo.InvariantCulture);
                }
                case X509FindType.FindByKeyUsage:
                {
                    if (int.TryParse(certificateFilterValue, out var keyUsages))
                        return keyUsages;
                    return certificateFilterValue;
                }
                case X509FindType.FindByThumbprint:
                case X509FindType.FindBySubjectName:
                case X509FindType.FindBySubjectDistinguishedName:
                case X509FindType.FindByIssuerName:
                case X509FindType.FindByIssuerDistinguishedName:
                case X509FindType.FindBySerialNumber:
                case X509FindType.FindByTemplateName:
                case X509FindType.FindByApplicationPolicy:
                case X509FindType.FindByCertificatePolicy:
                case X509FindType.FindByExtension:
                case X509FindType.FindBySubjectKeyIdentifier:
                {
                    return certificateFilterValue;
                }
                default:
                {
                    throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}