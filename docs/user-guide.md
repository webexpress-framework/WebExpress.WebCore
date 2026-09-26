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
