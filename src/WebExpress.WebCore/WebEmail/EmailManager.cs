using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MimeKit;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.WebEmail
{
    /// <summary>
    /// Centralizes delivery policy and atomically claims logical messages in the current cluster store.
    /// Claims survive failed or interrupted submissions because SMTP acceptance can be ambiguous.
    /// </summary>
    public sealed class EmailManager : IEmailManager, ISystemComponent
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly EmailSettings _settings;
        private readonly Dictionary<string, IEmailProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _sync = new();
        private bool _disposed;

        /// <summary>
        /// Binds deployment settings once while resolving the cluster store at submission time.
        /// </summary>
        /// <param name="componentHub">The hub supplying the current shared store.</param>
        /// <param name="httpServerContext">The host configuration, log and graceful shutdown lifetime.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private EmailManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
            _settings = httpServerContext.Configuration.GetSection("WebExpress:Email").Get<EmailSettings>() ?? new();
            if (_settings.DeduplicationHours is < 1 or > 8760 || _settings.MaxAttachmentBytes < 0)
            {
                throw new EmailException(EmailError.Configuration);
            }

            _settings.Profiles = new Dictionary<string, EmailProfileSettings>(_settings.Profiles ?? [], StringComparer.OrdinalIgnoreCase);
            _providers.Add("smtp", new SmtpEmailProvider());
        }

        /// <summary>
        /// Returns the settings bound by this manager and the currently registered providers.
        /// </summary>
        /// <returns>A detached snapshot without credentials or provider-specific options.</returns>
        public EmailStatus GetStatus()
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return new EmailStatus
                {
                    Enabled = _settings.Enabled,
                    DefaultProfile = _settings.DefaultProfile,
                    DeduplicationHours = _settings.DeduplicationHours,
                    MaxAttachmentBytes = _settings.MaxAttachmentBytes,
                    IsClustered = _componentHub.ClusterManager.IsClustered,
                    IsStoreShared = _componentHub.ClusterManager.Store.IsShared,
                    Profiles = Array.AsReadOnly(_settings.Profiles.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(x => CreateProfileInfo(x.Key, x.Value)).ToArray())
                };
            }
        }

        /// <summary>
        /// Projects only explicitly approved diagnostic values from a profile containing deployment secrets.
        /// </summary>
        /// <param name="name">The configured profile name.</param>
        /// <param name="profile">The bound profile, which may be incomplete.</param>
        /// <returns>A credential-free profile snapshot.</returns>
        private EmailProfileInfo CreateProfileInfo(string name, EmailProfileSettings profile)
        {
            var valid = profile is not null && !string.IsNullOrWhiteSpace(profile.Provider);
            if (valid)
            {
                try
                {
                    ValidateProfile(profile);
                }
                catch (EmailException)
                {
                    valid = false;
                }
            }
            var smtp = string.Equals(profile?.Provider, "smtp", StringComparison.OrdinalIgnoreCase);
            return new EmailProfileInfo
            {
                Name = name,
                Provider = profile?.Provider,
                From = profile?.From,
                Host = smtp ? profile.Host : null,
                Port = smtp ? profile.Port : null,
                Security = smtp ? profile.Security : null,
                TimeoutSeconds = profile?.TimeoutSeconds ?? 0,
                AuthenticationConfigured = smtp && (!string.IsNullOrEmpty(profile.UserName) || !string.IsNullOrEmpty(profile.Password)),
                ProviderRegistered = profile?.Provider is not null && _providers.ContainsKey(profile.Provider),
                ConfigurationValid = valid
            };
        }

        /// <summary>
        /// Adds a provider without allowing accidental replacement of an existing implementation.
        /// </summary>
        /// <param name="provider">The plugin-owned provider, which must support concurrent submissions.</param>
        public void RegisterProvider(IEmailProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentException.ThrowIfNullOrWhiteSpace(provider.Name);
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (!_providers.TryAdd(provider.Name, provider))
                {
                    throw new ArgumentException("The email provider name is already registered.", nameof(provider));
                }
            }
        }

        /// <summary>
        /// Removes only the specified plugin provider so a stale unload cannot remove its replacement.
        /// Already admitted operations retain their provider until completion.
        /// </summary>
        /// <param name="provider">The exact registered instance owned by the unloading plugin.</param>
        /// <returns>True when that instance was removed.</returns>
        public bool UnregisterProvider(IEmailProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return !string.Equals(provider.Name, "smtp", StringComparison.OrdinalIgnoreCase)
                    && _providers.TryGetValue(provider.Name, out var current)
                    && ReferenceEquals(current, provider) && _providers.Remove(provider.Name);
            }
        }

        /// <summary>
        /// Snapshots and validates application content before admitting the delivery to host shutdown tracking.
        /// </summary>
        /// <param name="applicationId">The stable namespace shared by all replicas of the application.</param>
        /// <param name="message">The content and stable logical delivery identifier.</param>
        /// <param name="profile">The configured profile name or null for the default.</param>
        /// <param name="cancellationToken">The caller cancellation signal.</param>
        /// <returns>Provider acceptance or suppression of an existing claim.</returns>
        public Task<EmailSendResult> SendAsync(string applicationId, EmailMessage message, string profile = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MimeMessage snapshot = null;
            try
            {
                EmailProfileSettings options;
                IEmailProvider provider;
                lock (_sync)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    if (!_settings.Enabled)
                    {
                        throw new EmailException(EmailError.Disabled);
                    }
                    if (!_settings.Profiles.TryGetValue(profile ?? _settings.DefaultProfile ?? "", out var configured)
                        || configured is null || string.IsNullOrWhiteSpace(configured.Provider)
                        || !_providers.TryGetValue(configured.Provider, out provider))
                    {
                        throw new EmailException(EmailError.Configuration);
                    }

                    options = JsonSerializer.Deserialize<EmailProfileSettings>(JsonSerializer.Serialize(configured));
                }

                ValidateProfile(options);
                string key;
                try
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
                    ArgumentNullException.ThrowIfNull(message);
                    ArgumentException.ThrowIfNullOrWhiteSpace(message.DeliveryId);
                    key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                        JsonSerializer.Serialize(new[] { applicationId, message.DeliveryId }))));
                    snapshot = CreateSnapshot(message, options, key);
                }
                catch (Exception ex) when (ex is ArgumentException or FormatException)
                {
                    throw new EmailException(EmailError.InvalidMessage, ex);
                }

                var completion = new TaskCompletionSource<EmailSendResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                var ownedSnapshot = snapshot;
                lock (_sync)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    if (!_httpServerContext.Lifetime.TryRun(async _ =>
                    {
                        using (ownedSnapshot)
                        {
                            try
                            {
                                completion.TrySetResult(await DeliverAsync(key, ownedSnapshot, options, provider, cancellationToken)
                                    .ConfigureAwait(false));
                            }
                            catch (OperationCanceledException)
                            {
                                _httpServerContext.Log?.Info($"email.cancelled correlation={key}");
                                completion.TrySetCanceled(cancellationToken);
                            }
                            catch (EmailException ex)
                            {
                                LogFailure(ex.Error, key, ex.InnerException);
                                completion.TrySetException(ex);
                            }
                            catch (Exception ex)
                            {
                                LogFailure(EmailError.DeliveryFailed, key, ex);
                                completion.TrySetException(new EmailException(EmailError.DeliveryFailed, ex));
                            }
                        }
                    }))
                    {
                        throw new EmailException(EmailError.Stopping);
                    }
                }

                snapshot = null;
                return completion.Task;
            }
            catch (EmailException ex)
            {
                LogFailure(ex.Error, "none", ex.InnerException);
                return Task.FromException<EmailSendResult>(ex);
            }
            finally
            {
                snapshot?.Dispose();
            }
        }

        /// <summary>
        /// Rejects invalid transport settings before a claim could suppress a corrected submission.
        /// </summary>
        /// <param name="profile">The selected private configuration snapshot.</param>
        private static void ValidateProfile(EmailProfileSettings profile)
        {
            if (profile.TimeoutSeconds is < 1 or > 300
                || (profile.Provider.Equals("smtp", StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrWhiteSpace(profile.Host) || profile.Port is < 1 or > 65535
                        || !Enum.IsDefined(profile.Security)
                        || (string.IsNullOrEmpty(profile.UserName) != string.IsNullOrEmpty(profile.Password))
                        || (profile.Security == EmailSecurity.None && !string.IsNullOrEmpty(profile.UserName)))))
            {
                throw new EmailException(EmailError.Configuration);
            }
        }

        /// <summary>
        /// Builds MIME content while copying caller-owned bytes and rejecting unsafe header inputs.
        /// </summary>
        /// <param name="message">The caller-owned logical message.</param>
        /// <param name="profile">The profile supplying a default sender.</param>
        /// <param name="key">The opaque delivery correlation used for the Message-Id header.</param>
        /// <returns>An owned MIME snapshot.</returns>
        private MimeMessage CreateSnapshot(EmailMessage message, EmailProfileSettings profile, string key)
        {
            var result = new MimeMessage();
            try
            {
                result.From.Add(ParseMailbox(message.From ?? profile.From));
                foreach (var address in message.To ?? [])
                {
                    result.To.Add(ParseMailbox(address));
                }
                foreach (var address in message.Cc ?? [])
                {
                    result.Cc.Add(ParseMailbox(address));
                }
                foreach (var address in message.Bcc ?? [])
                {
                    result.Bcc.Add(ParseMailbox(address));
                }
                if (result.To.Count + result.Cc.Count + result.Bcc.Count == 0
                    || (message.TextBody is null && message.HtmlBody is null)
                    || message.Subject?.IndexOfAny(['\r', '\n', '\0']) >= 0)
                {
                    throw new ArgumentException("A recipient, a body and a valid subject are required.");
                }

                if (message.ReplyTo is not null)
                {
                    result.ReplyTo.Add(ParseMailbox(message.ReplyTo));
                }
                result.Subject = message.Subject ?? "";
                result.MessageId = $"{key.ToLowerInvariant()}@webexpress.invalid";
                var body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody };
                long size = 0;
                foreach (var attachment in message.Attachments ?? [])
                {
                    if (attachment?.Content is null || string.IsNullOrWhiteSpace(attachment.FileName)
                        || attachment.FileName.IndexOfAny(['/', '\\', '\r', '\n', '\0']) >= 0
                        || string.IsNullOrWhiteSpace(attachment.ContentType)
                        || attachment.ContentType.IndexOfAny(['\r', '\n', '\0']) >= 0)
                    {
                        throw new ArgumentException("An attachment requires content, a filename and a media type.");
                    }
                    size += attachment.Content.LongLength;
                    if (size > _settings.MaxAttachmentBytes)
                    {
                        throw new ArgumentException("The combined attachment limit was exceeded.");
                    }
                    body.Attachments.Add(attachment.FileName, attachment.Content.ToArray(), ContentType.Parse(attachment.ContentType));
                }

                result.Body = body.ToMessageBody();
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Restricts each input to one mailbox and prevents untrusted values from adding header lines.
        /// </summary>
        /// <param name="value">The single mailbox input, optionally with a display name.</param>
        /// <returns>The parsed mailbox.</returns>
        private static MailboxAddress ParseMailbox(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(['\r', '\n', '\0']) >= 0
                || !MailboxAddress.TryParse(value, out var mailbox) || !mailbox.Address.Contains('@'))
            {
                throw new ArgumentException("A valid mailbox address is required.");
            }
            return mailbox;
        }

        /// <summary>
        /// Claims the attempt atomically before any external side effect and keeps uncertain outcomes claimed.
        /// </summary>
        /// <param name="key">The opaque application and delivery identifier hash.</param>
        /// <param name="message">The validated MIME snapshot.</param>
        /// <param name="profile">The private provider configuration.</param>
        /// <param name="provider">The provider retained for this admitted operation.</param>
        /// <param name="cancellationToken">The caller cancellation signal.</param>
        /// <returns>Acceptance or suppression without falsely reporting delivery for a duplicate.</returns>
        private async Task<EmailSendResult> DeliverAsync(string key, MimeMessage message, EmailProfileSettings profile,
            IEmailProvider provider, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(profile.TimeoutSeconds));
            bool claimed;
            try
            {
                var cluster = _componentHub.ClusterManager;
                var store = cluster.Store;
                if (cluster.IsClustered && !store.IsShared)
                {
                    throw new InvalidOperationException("Cluster email requires a shared store.");
                }
                claimed = store.TryAdd("email-attempt", key, [1], TimeSpan.FromHours(_settings.DeduplicationHours));
            }
            catch (Exception ex)
            {
                throw new EmailException(EmailError.StoreUnavailable, ex);
            }

            if (!claimed)
            {
                _httpServerContext.Log?.Info($"email.duplicate correlation={key}");
                return EmailSendResult.AlreadyAttempted;
            }

            _httpServerContext.Log?.Info($"email.started correlation={key}");
            try
            {
                deadline.Token.ThrowIfCancellationRequested();
                await provider.SendAsync(message, profile, deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
            {
                throw new EmailException(EmailError.Timeout);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new EmailException(EmailError.DeliveryFailed, ex);
            }

            _httpServerContext.Log?.Info($"email.accepted correlation={key}");
            return EmailSendResult.Accepted;
        }

        /// <summary>
        /// Emits technical categories without copying provider responses, credentials or message content into logs.
        /// </summary>
        /// <param name="error">The stable error category.</param>
        /// <param name="key">The opaque correlation hash or a fixed marker for validation failures.</param>
        /// <param name="cause">The diagnostic cause whose type is safe to record.</param>
        private void LogFailure(EmailError error, string key, Exception cause)
        {
            _httpServerContext.Log?.Error($"email.failed correlation={key} category={error} cause={cause?.GetType().Name ?? "none"}");
        }

        /// <summary>
        /// Closes admission after the host drains accepted work without disposing plugin-owned providers.
        /// </summary>
        public void Dispose()
        {
            lock (_sync)
            {
                _disposed = true;
                _providers.Clear();
            }
            GC.SuppressFinalize(this);
        }
    }
}
