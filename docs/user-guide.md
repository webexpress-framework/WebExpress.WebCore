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

The container health endpoint is documented in the [Health model](https://github.com/webexpress-framework/WebExpress/blob/main/docs/development_guide.md#health-model) section of the Development Guide. WebCore provides `/health` globally, and applications contribute critical dependency checks as public sealed `IHealth` components in `WebExpress.WebCore.WebHealt`. The `HealthManager` discovers these components through the plugin and application lifecycle. The guide includes the component model, a database example, the HTTP contract, and Docker and Kubernetes probe configuration.

## Graceful shutdown

The container lifecycle is described in the [Graceful shutdown guide](graceful-shutdown.md). It covers `Shutdown: "graceful"`, the shared drain budget, background synchronization, resource ownership, and Docker and Kubernetes termination settings.

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
