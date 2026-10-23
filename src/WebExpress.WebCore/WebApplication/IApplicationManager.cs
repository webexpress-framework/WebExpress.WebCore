using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebApplication
{
    /// <summary>
    /// Interface of the management of WebExpress applications.
    /// </summary>
    public interface IApplicationManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when an application is added.
        /// </summary>
        event EventHandler<IApplicationContext> AddApplication;

        /// <summary>
        /// An event that fires when an application is removed.
        /// </summary>
        event EventHandler<IApplicationContext> RemoveApplication;

        /// <summary>
        /// An event that fires when the name or the icon of a registered application changed.
        /// </summary>
        event EventHandler<IApplicationContext> UpdateApplication;

        /// <summary>
        /// Gets the stored applications.
        /// </summary>
        IEnumerable<IApplicationContext> Applications { get; }

        /// <summary>
        /// Gets the declared applications whose constructor threw, for as long as their plugin is loaded.
        /// </summary>
        /// <remarks>
        /// A failed application is absent from <see cref="Applications"/>, so nothing else in the
        /// host can tell that it was ever declared. The health probe reads this to stay unhealthy
        /// instead of reporting a host that lost its application as available.
        /// </remarks>
        IEnumerable<ApplicationFailure> FailedApplications { get; }

        /// <summary>
        /// Returns the application contexts for a given application id.
        /// </summary>
        /// <param name="applicationId">The application id.</param>
        /// <returns>The context of the application or null.</returns>
        IApplicationContext GetApplication(string applicationId);

        /// <summary>
        /// Returns the application contexts for a given application id.
        /// </summary>
        /// <typeparam name="T">The application type.</typeparam>
        /// <returns>The context of the application or null.</returns>
        IApplicationContext GetApplication<T>();

        /// <summary>
        /// Returns the application contexts for the given application ids.
        /// </summary>
        /// <param name="applicationIds">The applications ids. Can contain regular expressions or * for all.</param>
        /// <returns>The contexts of the applications as an enumeration.</returns>
        IEnumerable<IApplicationContext> GetApplications(IEnumerable<string> applicationIds);

        /// <summary>
        /// Returns the application contexts for the given plugin.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin.</param>
        /// <returns>The contexts of the applications as an enumeration.</returns>
        IEnumerable<IApplicationContext> GetApplications(IPluginContext pluginContext);

        /// <summary>
        /// Returns the application contexts for a given application type.
        /// </summary>
        /// <param name="application">The application type.</param>
        /// <returns>The contexts of the applications as an enumeration.</returns>
        IEnumerable<IApplicationContext> GetApplications(Type application);

        /// <summary>
        /// Replaces the display name of a registered application.
        /// </summary>
        /// <remarks>
        /// The name an application registers with comes from its <c>[Name]</c> attribute and is
        /// therefore fixed at compile time. An installation that wants to call the application
        /// something else - a tenant with its own branding, a deployment named after the team it
        /// serves - has no way to say so through an attribute, which is what this exists for. The
        /// application id is untouched: it identifies the application to the framework, while the
        /// name is only ever shown to a reader.
        /// </remarks>
        /// <param name="applicationContext">The context of the application to rename.</param>
        /// <param name="applicationName">The new name. A blank value restores the declared one.</param>
        void SetApplicationName(IApplicationContext applicationContext, string applicationName);

        /// <summary>
        /// Replaces the icon of a registered application.
        /// </summary>
        /// <remarks>
        /// The value is a path relative to the application, exactly as the <c>[Icon]</c> attribute
        /// declares it; the manager combines it with the server route and the context path the
        /// same way it does at registration, so a caller never has to know how the route is
        /// assembled.
        /// </remarks>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="icon">The new icon path. A blank value restores the declared one.</param>
        void SetApplicationIcon(IApplicationContext applicationContext, string icon);
    }
}
