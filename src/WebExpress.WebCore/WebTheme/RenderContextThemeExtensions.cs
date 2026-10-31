using System.Linq;
using WebExpress.WebCore.WebPage;

namespace WebExpress.WebCore.WebTheme
{
    /// <summary>
    /// Render-time extensions that resolve the active theme for the
    /// application carried in the render context.
    /// </summary>
    /// <remarks>
    /// The resolution order mirrors <c>VisualTreeControl</c> so server-side
    /// icon factories and the visual tree end up with the same theme:
    /// <list type="number">
    ///   <item><description>
    ///     The application's declared default theme (<c>[Theme&lt;T&gt;]</c> →
    ///     <c>IApplicationContext.DefaultTheme</c>).
    ///   </description></item>
    ///   <item><description>
    ///     The first theme registered for the application (legacy fallback).
    ///   </description></item>
    ///   <item><description>
    ///     <see langword="null"/>; callers then render without a theme.
    ///   </description></item>
    /// </list>
    /// Per-user overrides are wired by application code: the page's
    /// <c>Process</c> hook calls <c>visualTree.UseTheme&lt;TTheme&gt;()</c>
    /// based on whatever store the application keeps; the framework itself
    /// does not consult cookies, sessions, or identities.
    /// </remarks>
    public static class RenderContextThemeExtensions
    {
        /// <summary>
        /// Returns the active theme for the render context using the
        /// resolution order documented on the class.
        /// </summary>
        /// <param name="renderContext">The current render context.</param>
        /// <returns>The active theme context or <see langword="null"/>.</returns>
        public static IThemeContext GetActiveTheme(this IRenderContext renderContext)
        {
            var applicationContext = renderContext?.PageContext?.ApplicationContext;
            if (applicationContext is null)
            {
                return null;
            }

            // 1. application's declared default
            if (applicationContext.DefaultTheme is { } declared)
            {
                return declared;
            }

            // 2. first registered theme for the application
            return WebEx.ComponentHub?.ThemeManager?.Themes
                ?.FirstOrDefault(t => t.ApplicationContext == applicationContext);
        }
    }
}
