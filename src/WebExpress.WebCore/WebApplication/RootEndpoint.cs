using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebSetting;

namespace WebExpress.WebCore.WebApplication
{
    /// <summary>
    /// Provides a shared entry point for installations whose applications use dedicated paths.
    /// </summary>
    internal static class RootEndpoint
    {
        /// <summary>
        /// Selects an application or an overview while leaving application paths under normal routing.
        /// </summary>
        /// <param name="request">The request used to identify the entry point and localize the overview.</param>
        /// <param name="serverRoute">The deployment prefix that also acts as an entry point.</param>
        /// <param name="applications">The currently registered applications, including their effective names and routes.</param>
        /// <param name="settings">The optional redirect preferences for this installation.</param>
        /// <returns>The entry point response, or null when normal routing must handle the request.</returns>
        internal static IResponse Handle(IRequest request, IRoute serverRoute,
            IEnumerable<IApplicationContext> applications, RootSettings settings)
        {
            if (request.Method is not RequestMethod.GET and not RequestMethod.HEAD)
            {
                return null;
            }

            var path = new RouteEndpoint(request.Uri.PathSegments).ToString();
            if (path.Length != 0 && !string.Equals(path, serverRoute.ToString(), StringComparison.Ordinal))
            {
                return null;
            }

            var available = applications.ToArray();

            // an application mounted at the entry point must remain reachable without a redirect loop
            if (available.Any(x => string.Equals(x.Route.ToString(), path, StringComparison.Ordinal)))
            {
                return null;
            }

            IApplicationContext target = null;
            if (settings?.RedirectEnabled != false)
            {
                target = !string.IsNullOrWhiteSpace(settings?.ApplicationId)
                    ? available.FirstOrDefault(x => string.Equals(x.ApplicationId, settings.ApplicationId.Trim(), StringComparison.OrdinalIgnoreCase))
                    : available.Length == 1 ? available[0] : null;
            }

            var response = target is not null
                ? (IResponse)new ResponseMovedTemporarily(target.Route.ToUri())
                : CreateOverview(request, available);

            // browsers must reconsider the entry point after applications or preferences change
            response.Header.CacheControl = "no-store";
            if (request.Method == RequestMethod.HEAD)
            {
                response.Content = null;
            }

            return response;
        }

        /// <summary>
        /// Keeps the application selector independent of optional UI plugins and application assets.
        /// </summary>
        /// <param name="request">The request that supplies the reader's preferred culture.</param>
        /// <param name="applications">The applications whose effective names and paths are displayed.</param>
        /// <returns>An HTML response containing the application links or an empty state.</returns>
        private static IResponse CreateOverview(IRequest request, IApplicationContext[] applications)
        {
            var title = I18N.Translate(request, "webexpress.webcore:root.title");
            var introduction = I18N.Translate(request, applications.Length == 0
                ? "webexpress.webcore:root.empty"
                : "webexpress.webcore:root.introduction");
            var document = new HtmlElementRootHtml();
            document.AddUserAttribute("lang", request.Culture.TwoLetterISOLanguageName);
            document.Head.Title = WebUtility.HtmlEncode(title + " | WebExpress");
            document.Head.Meta = [new("viewport", "width=device-width, initial-scale=1")];
            document.Head.Styles = ["""
                :root { color-scheme: light dark; font-family: system-ui, sans-serif; }
                * { box-sizing: border-box; }
                body { margin: 0; background: #f5f7fa; color: #182437; }
                main { max-width: 52rem; margin: 0 auto; padding: clamp(1.5rem, 6vw, 5rem) 1.5rem; }
                .wx-root-brand { color: #52647a; font-weight: 600; letter-spacing: .04em; }
                h1 { margin: 1rem 0 .5rem; font-size: clamp(1.8rem, 5vw, 2.5rem); }
                p { line-height: 1.6; }
                ul { display: grid; gap: .8rem; list-style: none; margin: 2rem 0 0; padding: 0; }
                a { display: flex; flex-direction: column; gap: .4rem; padding: 1.2rem 1.4rem;
                    border: 1px solid #d5dce5; border-radius: .6rem; background: #fff;
                    color: #164f91; text-decoration: none; overflow-wrap: anywhere; }
                a:hover { border-color: #164f91; background: #f0f6ff; }
                a:focus-visible { outline: 3px solid #286bc0; outline-offset: 3px; }
                .wx-root-path { color: #52647a; font-size: .9rem; }
                @media (prefers-color-scheme: dark) {
                    body { background: #141c28; color: #e5ecf5; }
                    a { background: #1d293a; border-color: #44536a; color: #9ac8ff; }
                    a:hover { background: #26374d; border-color: #9ac8ff; }
                    .wx-root-brand, .wx-root-path { color: #b5c4d8; }
                }
                """];

            var list = new HtmlElementTextContentUl();
            foreach (var application in applications.OrderBy(x => I18N.Translate(request, x.ApplicationName), StringComparer.CurrentCultureIgnoreCase))
            {
                var name = I18N.Translate(request, application.ApplicationName);
                var path = application.Route.ToString();
                var link = new HtmlElementTextSemanticsA
                (
                    new HtmlElementTextSemanticsStrong(WebUtility.HtmlEncode(name)),
                    new HtmlElementTextSemanticsSpan(new HtmlText(WebUtility.HtmlEncode(path))) { Class = "wx-root-path" }
                )
                {
                    Href = path
                };
                list.Add(new HtmlElementTextContentLi(link));
            }

            document.Body.Add(new HtmlElementSectionMain
            (
                new HtmlElementTextContentP("WebExpress") { Class = "wx-root-brand" },
                new HtmlElementSectionH1(WebUtility.HtmlEncode(title)),
                new HtmlElementTextContentP(WebUtility.HtmlEncode(introduction)),
                list
            ));

            var response = new ResponseOK { Content = document };
            response.Header.ContentType = "text/html; charset=utf-8";
            return response;
        }
    }
}
