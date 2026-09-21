using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebLog;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.WebCertificate
{
    /// <summary>
    /// Owns certificate material and validates it before hosts or applications can use it.
    /// Store registration and inventory replacement are serialized with resolution.
    /// </summary>
    public sealed class CertificateManager : ICertificateManager
    {
        private readonly object _sync = new();
        private readonly ILog _log;
        private readonly TimeProvider _timeProvider;
        private readonly Dictionary<string, ICertificateStore> _stores = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<CertificateMaterial> _materials = [];
        private Dictionary<string, Entry> _lookup = new(StringComparer.OrdinalIgnoreCase);
        private Entry[] _entries = [];
        private int[] _thresholds = [];
        private bool _disposed;

        /// <summary>
        /// Keeps validated mappings separate from credentials that are needed only while loading.
        /// </summary>
        /// <param name="alias">The canonical configured alias.</param>
        /// <param name="store">The registered store name.</param>
        /// <param name="hostNames">The validated concrete hostname mappings.</param>
        /// <param name="material">The owned material, or null after a load failure.</param>
        /// <param name="status">The suitability checks that do not change with time.</param>
        private sealed class Entry(string alias, string store, string[] hostNames, CertificateMaterial material, CertificateStatus status)
        {
            internal readonly string Alias = alias;
            internal readonly string Store = store;
            internal readonly string[] HostNames = hostNames;
            internal readonly CertificateMaterial Material = material;
            internal readonly CertificateStatus Status = status;
        }

        /// <summary>
        /// Connects validation to the server log and an injectable UTC clock.
        /// </summary>
        /// <param name="log">The shared log, or null when diagnostics are not required.</param>
        /// <param name="timeProvider">The clock used for validity checks, defaulting to the system clock.</param>
        public CertificateManager(ILog log = null, TimeProvider timeProvider = null)
        {
            _log = log;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <summary>
        /// Registers a store for use by future inventory loads. The "file" store is reserved for the built-in file system provider.
        /// </summary>
        /// <param name="store">The certificate store to register.</param>
        /// <exception cref="ArgumentException">Thrown when the store name is already registered or reserved.</exception>
        public void RegisterStore(ICertificateStore store)
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentException.ThrowIfNullOrWhiteSpace(store.Name);
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (store.Name.Equals("file", StringComparison.OrdinalIgnoreCase) || !_stores.TryAdd(store.Name, store))
                {
                    throw new ArgumentException("The certificate store name is already registered or reserved.", nameof(store));
                }
            }
        }

        /// <summary>
        /// Replaces the inventory with a new set of registrations and validates them for immediate use.
        /// </summary>
        /// <param name="settings">The HTTP server settings containing certificate configurations.</param>
        /// <exception cref="ArgumentException">Thrown when certificate warning thresholds are negative.</exception>
        public void Load(HttpServerSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                var options = settings.Certificates ?? new CertificateManagerSettings();
                var thresholds = (options.WarningThresholdDays ?? [30, 14, 7]).Distinct().Order().ToArray();
                if (thresholds.Any(x => x < 0))
                {
                    throw new ArgumentException("Certificate warning thresholds must not be negative.", nameof(settings));
                }

                var registrations = GetRegistrations(settings);
                var fileStore = new FileCertificateStore(options.Directory);
                var lookup = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
                var entries = new List<Entry>();
                try
                {
                    foreach (var registration in registrations)
                    {
                        var store = registration.Store.Equals("file", StringComparison.OrdinalIgnoreCase)
                            ? fileStore : _stores.GetValueOrDefault(registration.Store);
                        if (store is null)
                        {
                            throw new InvalidOperationException($"Certificate store '{registration.Store}' is not registered.");
                        }

                        var entry = LoadEntry(registration, store);
                        entries.Add(entry);
                        foreach (var key in entry.HostNames.Prepend(entry.Alias).Distinct(StringComparer.OrdinalIgnoreCase))
                        {
                            if (!lookup.TryAdd(key, entry))
                            {
                                throw new InvalidOperationException($"Certificate alias or hostname '{key}' is ambiguous.");
                            }
                        }
                    }
                }
                catch
                {
                    foreach (var entry in entries) { entry.Material?.Dispose(); }
                    throw;
                }

                // borrowed material may still be used by a listener after an explicit inventory reload
                _materials.AddRange(entries.Where(x => x.Material is not null).Select(x => x.Material));
                _entries = entries.ToArray();
                _lookup = lookup;
                _thresholds = thresholds;

                foreach (var entry in _entries)
                {
                    var info = GetInfo(entry);
                    if (!info.IsUsable)
                    {
                        _log?.Warning(Translate("webexpress.webcore:certificate.invalid",
                            "Certificate {0} cannot be used for HTTPS: {1}.", info.Alias, info.Status));
                    }
                    else if (info.Status.HasFlag(CertificateStatus.ExpiringSoon))
                    {
                        var remainingDays = (info.NotAfter.Value - _timeProvider.GetUtcNow()).TotalDays;
                        var threshold = thresholds.First(x => remainingDays <= x);
                        _log?.Warning(Translate("webexpress.webcore:certificate.expiring",
                            "Certificate {0} expires at {1}; its remaining lifetime reached the warning threshold of {2} days.", info.Alias,
                            info.NotAfter.Value.ToString("O", CultureInfo.InvariantCulture), threshold));
                    }
                }
            }
        }

        /// <summary>
        /// Resolves a certificate by its alias or a concrete hostname that is covered by the certificate's subject alternative names.
        /// </summary>
        /// <param name="aliasOrHostName">The alias or hostname of the certificate to resolve.</param>
        /// <returns>The resolved certificate material.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when the certificate is not configured.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the certificate cannot be used for HTTPS.</exception>
        public CertificateMaterial Resolve(string aliasOrHostName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(aliasOrHostName);
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (!_lookup.TryGetValue(NormalizeKey(aliasOrHostName), out var entry))
                {
                    throw new KeyNotFoundException($"Certificate '{aliasOrHostName}' is not configured.");
                }

                var info = GetInfo(entry);
                if (!info.IsUsable)
                {
                    throw new InvalidOperationException($"Certificate '{info.Alias}' cannot be used for HTTPS: {info.Status}.");
                }

                return entry.Material;
            }
        }

        /// <summary>
        /// Resolves a certificate for an HTTPS endpoint, ensuring that the certificate covers the endpoint's hostname.
        /// </summary>
        /// <param name="endpoint">The HTTPS endpoint for which to resolve a certificate.</param>
        /// <returns>The resolved certificate material.</returns>
        /// <exception cref="ArgumentException">Thrown when the endpoint is not an HTTPS endpoint.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the certificate does not cover the endpoint's hostname.</exception>
        public CertificateMaterial Resolve(EndpointSettings endpoint)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            var uri = endpoint.GetBindingAddress();
            if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Certificate resolution requires an HTTPS endpoint.", nameof(endpoint));
            }

            var material = Resolve(GetEndpointKey(endpoint));
            if (uri.Host != "*" && !material.Certificate.MatchesHostname(NormalizeHostName(uri.Host), allowCommonName: false))
            {
                throw new InvalidOperationException($"The certificate does not cover HTTPS endpoint '{endpoint.Uri}'.");
            }

            return material;
        }

        /// <summary>
        /// Returns a snapshot of the current inventory with validity and expiry warnings evaluated at the current time.
        /// </summary>
        /// <returns>A read-only list of certificate information.</returns>
        public IReadOnlyList<CertificateInfo> GetCertificates()
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return Array.AsReadOnly(_entries.Select(GetInfo).ToArray());
            }
        }

        /// <summary>
        /// Combines shared definitions with inline endpoint definitions without modifying bound settings.
        /// </summary>
        /// <param name="settings">The configuration to convert into unique registrations.</param>
        /// <returns>The registrations with normalized aliases and concrete hostname mappings.</returns>
        private static IEnumerable<CertificateSettings> GetRegistrations(HttpServerSettings settings)
        {
            var registrations = new Dictionary<string, CertificateSettings>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in settings.Certificates?.Items ?? [])
            {
                ArgumentNullException.ThrowIfNull(item);
                ArgumentException.ThrowIfNullOrWhiteSpace(item.Alias);
                ArgumentException.ThrowIfNullOrWhiteSpace(item.Store);
                ArgumentException.ThrowIfNullOrWhiteSpace(item.Reference);
                var registration = new CertificateSettings
                {
                    Alias = NormalizeKey(item.Alias),
                    Store = item.Store,
                    Reference = item.Reference,
                    Password = item.Password,
                    HostNames = (item.HostNames ?? []).Select(NormalizeHostName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                };
                if (!registrations.TryAdd(registration.Alias, registration))
                {
                    throw new InvalidOperationException($"Certificate alias '{registration.Alias}' is duplicated.");
                }
            }

            foreach (var endpoint in settings.Endpoints ?? [])
            {
                var uri = endpoint.GetBindingAddress();
                if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) { continue; }
                var key = NormalizeKey(GetEndpointKey(endpoint));
                registrations.TryGetValue(key, out var registration);
                if (!string.IsNullOrWhiteSpace(endpoint.PfxFile))
                {
                    if (registration is not null && (!registration.Store.Equals("file", StringComparison.OrdinalIgnoreCase) ||
                        registration.Reference != endpoint.PfxFile || registration.Password != endpoint.Password))
                    {
                        throw new InvalidOperationException($"Certificate alias '{key}' has conflicting definitions.");
                    }
                    if (registration is null)
                    {
                        registration = new CertificateSettings
                        {
                            Alias = key,
                            Reference = endpoint.PfxFile,
                            Password = endpoint.Password
                        };
                        registrations.Add(key, registration);
                    }
                }
            }

            // resolve mappings after all inline declarations so configuration order cannot hide a hostname
            foreach (var endpoint in settings.Endpoints ?? [])
            {
                var uri = endpoint.GetBindingAddress();
                if (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) && uri.Host != "*" &&
                    registrations.TryGetValue(NormalizeKey(GetEndpointKey(endpoint)), out var registration))
                {
                    registration.HostNames = registration.HostNames.Append(NormalizeHostName(uri.Host))
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                }
            }

            return registrations.Values;
        }

        /// <summary>
        /// Preserves inline endpoint identity when no explicit alias was supplied.
        /// </summary>
        /// <param name="endpoint">The endpoint whose lookup identity is needed.</param>
        /// <returns>The alias, inline endpoint identity, or concrete hostname.</returns>
        private static string GetEndpointKey(EndpointSettings endpoint)
        {
            if (!string.IsNullOrWhiteSpace(endpoint.CertificateAlias)) { return endpoint.CertificateAlias; }
            return !string.IsNullOrWhiteSpace(endpoint.PfxFile) ? endpoint.Uri : endpoint.GetBindingAddress().Host;
        }

        /// <summary>
        /// Converts a store failure into inspectable status while excluding store exception text from logs.
        /// </summary>
        /// <param name="registration">The credential-bearing registration used only for this load.</param>
        /// <param name="store">The provider selected by the registration.</param>
        /// <returns>The loaded entry or a metadata-only failure entry.</returns>
        private Entry LoadEntry(CertificateSettings registration, ICertificateStore store)
        {
            CertificateMaterial material = null;
            try
            {
                material = store.Load(registration.Reference, registration.Password)
                    ?? throw new InvalidOperationException("The certificate store returned no material.");
                var status = Validate(material.Certificate, registration.HostNames);
                return new Entry(registration.Alias, store.Name, registration.HostNames, material, status);
            }
            catch (Exception)
            {
                // provider exception messages can contain credentials or secret store request details
                material?.Dispose();
                _log?.Error(Translate("webexpress.webcore:certificate.load_failed",
                    "Certificate {0} could not be loaded from store {1}. Check the reference, access permissions, PFX content and credential.",
                    registration.Alias, store.Name));
                return new Entry(registration.Alias, store.Name, registration.HostNames, null, CertificateStatus.LoadFailed);
            }
        }

        /// <summary>
        /// Enforces local TLS suitability without depending on network trust or revocation services.
        /// </summary>
        /// <param name="certificate">The leaf certificate to validate.</param>
        /// <param name="hostNames">The concrete hostnames that must be covered by subject alternative names.</param>
        /// <returns>The combined suitability failures independent of the current time.</returns>
        private static CertificateStatus Validate(X509Certificate2 certificate, IEnumerable<string> hostNames)
        {
            var status = certificate.HasPrivateKey ? CertificateStatus.Valid : CertificateStatus.MissingPrivateKey;
            if (certificate.Extensions.OfType<X509BasicConstraintsExtension>().Any(x => x.CertificateAuthority) ||
                certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().Any(x =>
                    !x.EnhancedKeyUsages.Cast<System.Security.Cryptography.Oid>().Any(oid => oid.Value == "1.3.6.1.5.5.7.3.1")) ||
                certificate.Extensions.OfType<X509KeyUsageExtension>().Any(x =>
                    (x.KeyUsages & X509KeyUsageFlags.DigitalSignature) == 0))
            {
                status |= CertificateStatus.InvalidUsage;
            }

            if (hostNames.Any(x => !certificate.MatchesHostname(x, allowCommonName: false)))
            {
                status |= CertificateStatus.HostNameMismatch;
            }

            return status;
        }

        /// <summary>
        /// Rechecks time on every read so a certificate cannot remain usable merely because startup succeeded.
        /// </summary>
        /// <param name="entry">The immutable inventory entry.</param>
        /// <returns>The current metadata snapshot.</returns>
        private CertificateInfo GetInfo(Entry entry)
        {
            var certificate = entry.Material?.Certificate;
            var status = entry.Status;
            DateTimeOffset? notBefore = certificate?.NotBefore.ToUniversalTime();
            DateTimeOffset? notAfter = certificate?.NotAfter.ToUniversalTime();
            var now = _timeProvider.GetUtcNow();
            if (notBefore.HasValue && now < notBefore.Value) { status |= CertificateStatus.NotYetValid; }
            if (notAfter.HasValue)
            {
                if (now >= notAfter.Value) { status |= CertificateStatus.Expired; }
                else if (_thresholds.Any(x => (notAfter.Value - now).TotalDays <= x)) { status |= CertificateStatus.ExpiringSoon; }
            }

            return new CertificateInfo
            {
                Alias = entry.Alias,
                Store = entry.Store,
                HostNames = Array.AsReadOnly(entry.HostNames),
                Subject = certificate?.Subject,
                Issuer = certificate?.Issuer,
                Thumbprint = certificate?.Thumbprint,
                NotBefore = notBefore,
                NotAfter = notAfter,
                Status = status
            };
        }

        /// <summary>
        /// Canonicalizes concrete hostnames so lookup and certificate coverage use identical identities.
        /// </summary>
        /// <param name="hostName">The configured DNS name or IP address without a port.</param>
        /// <returns>The ASCII DNS name or canonical IP address.</returns>
        private static string NormalizeHostName(string hostName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hostName);
            var value = hostName.Trim().TrimEnd('.');
            if (IPAddress.TryParse(value.Trim('[', ']'), out var address)) { return address.ToString(); }
            value = new IdnMapping().GetAscii(value);
            if (Uri.CheckHostName(value) != UriHostNameType.Dns)
            {
                throw new ArgumentException("Certificate host mappings require concrete DNS names or IP addresses.", nameof(hostName));
            }
            return value.ToLowerInvariant();
        }

        /// <summary>
        /// Makes DNS aliases and hostname lookups agree while retaining non-DNS aliases such as endpoint URIs.
        /// </summary>
        /// <param name="value">The alias or hostname to use as a dictionary key.</param>
        /// <returns>The canonical lookup key.</returns>
        private static string NormalizeKey(string value)
        {
            value = value.Trim().TrimEnd('.');
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            return Uri.CheckHostName(value.Trim('[', ']')) != UriHostNameType.Unknown ? NormalizeHostName(value) : value;
        }

        /// <summary>
        /// Keeps startup diagnostics actionable when the manager is used before translation resources are registered.
        /// </summary>
        /// <param name="key">The translation key in the Core resources.</param>
        /// <param name="fallback">The English format used when resources are unavailable.</param>
        /// <param name="args">The non-secret values included in the diagnostic.</param>
        /// <returns>The formatted diagnostic in the available language.</returns>
        private static string Translate(string key, string fallback, params object[] args)
        {
            var format = I18N.Translate(key);
            return string.Format(CultureInfo.CurrentCulture, format == key ? fallback : format, args);
        }

        /// <summary>
        /// Releases active and retired material after hosting has stopped using borrowed certificates.
        /// Registered stores remain owned by their supplying modules.
        /// </summary>
        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) { return; }
                _disposed = true;
                foreach (var material in _materials) { material.Dispose(); }
                _materials.Clear();
                _entries = [];
                _lookup.Clear();
                _stores.Clear();
            }
        }
    }
}
