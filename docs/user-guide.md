![WebExpress](https://raw.githubusercontent.com/webexpress-framework/.github/main/docs/assets/img/banner.png)

# User guide
Welcome to the `WebExpress.WebCore` User Guide. This guide will help you get started with `WebExpress.WebCore` and make the most out of its 
features. Follow the links below to begin your journey.

# Getting started
To get started with `WebExpress.WebCore`, use the following guides:

- [Installation Guide](https://github.com/webexpress-framework/WebExpress/blob/main/docs/installation_guide.md) 
- [Development Guide](https://github.com/webexpress-framework/WebExpress/blob/main/docs/development_guide.md)
- [WebExpress.WebCore API Documentation](https://webexpress-framework.github.io/WebExpress.WebCore/) 
- [WebExpress.WebUI API Documentation](https://webexpress-framework.github.io/WebExpress.WebUI/) 
- [WebExpress.WebApp API Documentation](https://webexpress-framework.github.io/WebExpress.WebApp/) 
- [WebExpress.WebIndex API Documentation](https://webexpress-framework.github.io/WebExpress.WebIndex/) 

We hope you enjoy using `WebExpress.WebCore` and find it valuable for your projects. Happy coding!

## Health checks

The container health endpoint is documented in the [Health model](https://github.com/webexpress-framework/WebExpress/blob/main/docs/development_guide.md#health-model) section of the Development Guide. WebCore provides `/health` globally, and applications contribute critical dependency checks as public sealed `IHealth` components in `WebExpress.WebCore.WebHealth`. The `HealthManager` discovers these components through the plugin and application lifecycle. The guide includes the component model, a database example, the HTTP contract, and Docker and Kubernetes probe configuration.

## Metrics

The Prometheus endpoint is documented in the [Metrics model](https://github.com/webexpress-framework/WebExpress/blob/main/docs/development_guide.md#metrics-model) section of the Development Guide. WebCore provides `/metrics` globally with request, error, login, active user, process, and runtime metrics. Applications record their own values with `MetricCounter`, `MetricGauge`, and `MetricHistogram` instruments, or report them at scrape time as public sealed `IMetric` components in `WebExpress.WebCore.WebMetrics`. The `MetricsManager` discovers these components through the plugin and application lifecycle and labels every series with its application. The guide includes an LDAP example, the list of framework metrics, the HTTP contract, and Prometheus, Kubernetes, and alerting configuration.

The endpoint is switched off by default, because the metrics reveal load and login failures to anyone who can reach the listener. Enable it explicitly, and set a scrape token when the listener is reachable from outside the cluster:

```json
{
  "WebExpress": {
    "Metrics": {
      "Enabled": true,
      "BearerToken": "a-long-random-secret"
    }
  }
}
```

The equivalent environment variables are `WEBEXPRESS_WebExpress__Metrics__Enabled` and `WEBEXPRESS_WebExpress__Metrics__BearerToken`. While the endpoint is off, `/metrics` is left to application routing. `Metrics:ActiveUserWindowMinutes` sets how recently a user must have sent an authenticated request to count as active (default `5`).

## Graceful shutdown

The container lifecycle is described in the [Graceful shutdown guide](graceful-shutdown.md). It covers `Shutdown: "graceful"`, the shared drain budget, background synchronization, resource ownership, and Docker and Kubernetes termination settings.

## Horizontal scaling

Several instances can serve one deployment behind a load balancer. The [Cluster model](https://github.com/webexpress-framework/WebExpress/blob/main/docs/development_guide.md#cluster-model) section of the Development Guide lists what each subsystem shares, the `WebExpress:Cluster` settings (shared state directory, peers, secret), `ShutdownDelaySeconds`, the store and transport extensions in `WebExpress.WebCore.WebCluster`, and Kubernetes and Docker Compose examples.

## Public server URI

Configure `WebExpress:ExternalUri` when the listener binding is not the URL used by clients, for example when WebExpress runs behind a reverse proxy. The server continues to bind to the addresses in `Endpoints`, while applications and components can obtain the public URL through `IHttpServerContext.ExternalUri`.

```json
{
  "WebExpress": {
	"Endpoints": [
	  { "Uri": "http://0.0.0.0:8080/" }
	],
	"ExternalUri": "https://www.example.com/"
  }
}
```

`ExternalUri` is optional. Leave it unset when the listener address is also the public URL.

## Server entry point

By default, a `GET` or `HEAD` request to `/` redirects to the application path when exactly one application is registered. With several applications, the server displays an overview containing their current names and links. With no applications, the overview displays an empty state. The overview supports English and German according to the request culture and requires no UI plugin.

For deployments with a `ContextPath`, the same behavior is available at that prefix with or without a trailing slash. Generated links and redirect targets include the prefix. Application URLs and their authentication checks remain unchanged. An application already mounted directly at an entry point retains that route, including when redirects are disabled.

To select one application even when several are installed, configure `WebExpress:Root:ApplicationId` in the active host's `settings/webexpress.settings.json`. The value is the registered application identifier exposed by `IApplicationContext.ApplicationId`, which is the application class's fully qualified name in lowercase. It is not the display name or URL. An unknown identifier displays the overview instead of redirecting to an arbitrary target.

```json
{
  "WebExpress": {
    "Root": {
      "ApplicationId": "example.plugin.application"
    }
  }
}
```

To always display the overview, set `WebExpress:Root:RedirectEnabled` to `false`. This setting takes precedence over `ApplicationId`. Omitting the `Root` block restores automatic selection. Apply configuration changes by restarting the host.

```json
{
  "WebExpress": {
    "Root": {
      "RedirectEnabled": false
    }
  }
}
```

For container configuration, the equivalent environment variables are `WEBEXPRESS_WebExpress__Root__ApplicationId` and `WEBEXPRESS_WebExpress__Root__RedirectEnabled`. Redirects use HTTP 302 with `Cache-Control: no-store` so application changes are reflected on the next visit. Root query parameters do not override the configured target or propagate to application links.

## Email delivery

For administration in a WebApp application, open **Settings > System > Email**. The page and its navigation entry are available only when the running manager has `WebExpress:Email:Enabled` set to `true`, and require the existing system access policy. An omitted or disabled email block hides the entry and makes the route unavailable. The page displays effective policy values, SMTP profiles and current provider registration without account names, passwords or provider-specific options. It remains read-only; configuration changes require a restart. Local status does not test network connectivity or delivery.

For application mail, use `IComponentHub.EmailManager` from `WebExpress.WebCore.WebEmail`. The shared manager supports plain text, HTML, alternative text and HTML bodies, To, Cc, Bcc, Reply-To and binary attachments. The built-in SMTP provider uses MailKit, and plugins can register other delivery providers through `IEmailProvider`. The [Email model](https://github.com/webexpress-framework/WebExpress/blob/main/docs/development_guide.md#email-model) describes the contracts and lifecycle.

For SMTP configuration, add an `Email` block inside `WebExpress` in the host's settings JSON. Email is disabled when the block is omitted or `Enabled` is false. Profile names and provider names are case insensitive. Settings are bound when the component hub is created, so changes require a host restart.

```json
{
  "WebExpress": {
    "Email": {
      "Enabled": true,
      "DefaultProfile": "default",
      "DeduplicationHours": 168,
      "MaxAttachmentBytes": 26214400,
      "Profiles": {
        "default": {
          "Provider": "smtp",
          "From": "Example Service <service@example.org>",
          "Host": "smtp.example.org",
          "Port": 587,
          "Security": "StartTls",
          "TimeoutSeconds": 60
        }
      }
    }
  }
}
```

For authentication, supply `WEBEXPRESS_WebExpress__Email__Profiles__default__UserName` and `WEBEXPRESS_WebExpress__Email__Profiles__default__Password` through deployment secrets. Leave both unset for a relay that does not require authentication. The built-in provider uses username and password authentication; OAuth token acquisition and provider HTTP APIs belong in custom providers. `Options` holds provider-specific string values and is passed only to the selected implementation.

For transport protection, `StartTls` requires the server to advertise and complete STARTTLS. `SslOnConnect` starts TLS immediately and normally uses port 465. Normal server certificate and hostname validation remain enabled. `None` is intended for an explicitly selected local development relay and rejects configured credentials. The mail TLS settings do not change the HTTP development listener.

For submitting a notification, inject `IEmailManager` into a framework component or use the component hub property. Pass `applicationContext.ApplicationId.ToString()` as the stable application namespace. Persist and reuse a business delivery identifier when several replicas can process the same event. A newly constructed `EmailMessage` otherwise gets a new random identifier.

```csharp
using WebExpress.WebCore.WebEmail;

var message = new EmailMessage
{
    DeliveryId = "invoice-1042-notification-v1",
    To = ["Customer <customer@example.org>"],
    Subject = "Your invoice",
    TextBody = "Your invoice is attached.",
    HtmlBody = "<p>Your invoice is attached.</p>",
    Attachments =
    [
        new EmailAttachment
        {
            FileName = "invoice.txt",
            ContentType = "text/plain",
            Content = System.Text.Encoding.UTF8.GetBytes("Invoice 1042")
        }
    ]
};

EmailSendResult result = await emailManager.SendAsync
(
    applicationContext.ApplicationId.ToString(),
    message,
    cancellationToken: cancellationToken
);
```

For content ownership, the manager copies attachment bytes and recipient values before asynchronous work starts. Callers retain ownership of their data and must not mutate it during the call that creates the snapshot. Set only `TextBody` for plain text, only `HtmlBody` for HTML, or both for a MIME alternative. The application escapes untrusted values when generating HTML. Attachment filenames cannot contain directory separators or header control characters. The combined unencoded attachment limit defaults to 25 MiB; MIME encoding increases the transmitted size, and the provider can impose a smaller limit.

For named profiles, supply `profile: "transactional"` to `SendAsync` and configure the corresponding entry under `Profiles`. A message can override `From` and set `ReplyTo`. Profile selection is a routing convenience for trusted application code, not a permission boundary. Changing profiles does not bypass a claim for the same application and delivery identifier.

For delivery results, `Accepted` means the provider accepted the submission, not that it reached an inbox. `AlreadyAttempted` means the same logical delivery was already claimed and may still be running, may have succeeded, or may have an uncertain outcome. The API performs direct asynchronous submission and does not provide a durable outbox, background queue, bounce processing or automatic retries.

For failure handling, catch `EmailException` and inspect `Error`. `Disabled`, `Configuration`, `InvalidMessage`, `StoreUnavailable` and `Stopping` stop submission before calling the provider. `DeliveryFailed` and `Timeout` may follow partial recipient acceptance or a lost server acknowledgement. Caller cancellation uses `OperationCanceledException`. Do not expose `InnerException` to HTTP clients because provider diagnostics can contain sensitive data. The central log records `email.started`, `email.accepted`, `email.duplicate`, `email.cancelled` and `email.failed` with an opaque correlation hash; error entries contain the category and cause type without bodies, addresses, subject lines or credentials.

For cluster deployments, configure a shared `Cluster:StatePath` or install a shared `IClusterStore` on every replica. The manager atomically claims the application and delivery identifier before contacting the provider. A cluster configured with peers but without a shared store rejects sending. Standalone deployments use the in-memory store unless configured otherwise, so claims disappear on process restart. A shared file store preserves claims across restarts and must use a filesystem with the atomic operations described in the Cluster model.

For duplicate protection, claims remain for `DeduplicationHours`, including failed, cancelled and interrupted attempts. The default is 168 hours and the supported range is 1 through 8760 hours. A crash after claiming but before submission can therefore suppress an unsent message. After expiry the same identifier can be sent again. This policy prevents repeated attempts during the retention window but does not promise exactly-once delivery. Applications requiring durable recovery should retain an outbox in their primary database and reconcile uncertain outcomes with the provider before creating a new delivery identifier.

For graceful shutdown, admitted sends participate in `ServerLifetime` draining, and new sends are rejected when draining starts. The per-profile delivery deadline defaults to 60 seconds and accepts values from 1 through 300 seconds. Select `Shutdown: "graceful"` and a sufficient shutdown budget when accepted messages should finish before shared resources are released. Immediate termination or an exhausted shutdown budget can leave the delivery outcome unknown.
