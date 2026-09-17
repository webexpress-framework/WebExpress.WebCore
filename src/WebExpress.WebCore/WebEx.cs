using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Configuration;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebLog;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPackage;
using WebExpress.WebCore.WebSetting;

[assembly: InternalsVisibleTo("WebExpress.WebCore.Test")]

namespace WebExpress.WebCore
{
    /// <summary>
    /// The class provides a web server application for WebExpress.
    /// </summary>
    public sealed class WebEx
    {
        private static IComponentHub _componentHub;
        private HttpServer _httpServer;

        /// <summary>
        /// Occurs when the initialization process is completed.
        /// </summary>
        /// <remarks>
        /// Subscribe to this event to perform actions after the initialization is
        /// finished.
        /// </remarks>
        public event EventHandler Initialization;

        /// <summary>
        /// Occurs when the start action is triggered.
        /// </summary>
        /// <remarks>
        /// Subscribe to this event to perform actions when the start process begins.
        /// </remarks>
        public event EventHandler Start;

        /// <summary>
        /// Occurs when the application is about to exit.
        /// </summary>
        /// <remarks>
        /// This event is raised just before the application shuts down.  It provides an
        /// opportunity to perform any necessary cleanup operations.
        /// </remarks>
        public event EventHandler Exit;

        /// <summary>
        /// Gets or sets the name of the web server.
        /// </summary>
        public string Name { get; set; } = "WebExpress";

        /// <summary>
        /// Gets the program version.
        /// </summary>
        public static string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString();

        /// <summary>
        /// Gets the component hub.
        /// </summary>
        public static IComponentHub ComponentHub => _componentHub;

        /// <summary>
        /// Gets the request currently being served on this call chain, or <see langword="null"/>
        /// outside a request.
        /// </summary>
        /// <remarks>
        /// The request is handed to endpoints, pages, controls and fragments, and passing it on
        /// from there is the right way to reach it - a method that needs the request should say
        /// so in its signature. This exists for the layers where that is not possible: a
        /// manager, a component or a store several calls deep that has to answer a question
        /// about the caller - who is signed in, which language they read, where they are
        /// connecting from - and whose signature is shared with callers that have no request at
        /// all. Threading a request through every one of them would mean changing every
        /// implementation of an interface for the sake of one of them.
        /// <para>
        /// It is an async local set for the duration of one request, so a call chain sees the
        /// request it belongs to and two requests served at once never see each other's. It is
        /// null outside a request - during startup, on a background worker, in a test - and
        /// callers have to answer that case rather than assume a request.
        /// </para>
        /// </remarks>
        public static IRequest CurrentRequest => _currentRequest.Value;

        /// <summary>
        /// The backing store of <see cref="CurrentRequest"/>.
        /// </summary>
        private static readonly AsyncLocal<IRequest> _currentRequest = new();

        /// <summary>
        /// Makes the supplied request the current one until the returned scope is closed.
        /// </summary>
        /// <remarks>
        /// Called by the server around the handling of one request. It is internal because the
        /// span of a request is the server's to decide: a host that could open the scope itself
        /// could also leave it open, and every layer reading <see cref="CurrentRequest"/> would
        /// then be told about a request that had long been answered.
        /// </remarks>
        /// <param name="request">The request being served.</param>
        /// <returns>The scope. Closing it restores what was current before.</returns>
        internal static IDisposable BeginRequest(IRequest request)
        {
            var previous = _currentRequest.Value;

            _currentRequest.Value = request;

            return new RequestScope(previous);
        }

        /// <summary>
        /// The scope handed out by <see cref="BeginRequest"/>.
        /// </summary>
        /// <param name="previous">The request that was current when the scope was opened.</param>
        private sealed class RequestScope(IRequest previous) : IDisposable
        {
            private bool _closed;

            /// <summary>
            /// Restores the request of the enclosing scope.
            /// </summary>
            public void Dispose()
            {
                if (_closed)
                {
                    return;
                }

                _closed = true;
                _currentRequest.Value = previous;
            }
        }

        /// <summary>
        /// Gets or sets the path to the favicon image used by the application.
        /// </summary>
        public static string Favicon { get; set; } = "webexpress.webui/assets/img/webexpress.svg";

        /// <summary>
        /// Running the application.
        /// </summary>
        /// <param name="args">Call arguments.</param>
        /// <returns>The return code. 0 on success. A number greater than 0 for errors.</returns>
        public int Execution(string[] args)
        {
            // prepare call arguments
            ArgumentParser.Current.Register(new ArgumentParserCommand() { FullName = "help", ShortName = "h" });
            ArgumentParser.Current.Register(new ArgumentParserCommand() { FullName = "config", ShortName = "c" });
            ArgumentParser.Current.Register(new ArgumentParserCommand() { FullName = "port", ShortName = "p" });
            ArgumentParser.Current.Register(new ArgumentParserCommand() { FullName = "spec", ShortName = "s" });
            ArgumentParser.Current.Register(new ArgumentParserCommand() { FullName = "output", ShortName = "o" });
            ArgumentParser.Current.Register(new ArgumentParserCommand() { FullName = "target", ShortName = "t" });

            // parsing call arguments
            var argumentDict = ArgumentParser.Current.Parse(args);

            if (argumentDict.ContainsKey("help"))
            {
                Console.WriteLine(Name + " [-port number | -config filename | -help]");
                Console.WriteLine("Version: " + Version);

                return 0;
            }

            // package builder
            if (argumentDict.ContainsKey("spec") || argumentDict.ContainsKey("output"))
            {
                if (!argumentDict.ContainsKey("spec"))
                {
                    Console.WriteLine("*** PackageBuilder: The spec file (-s) was not specified.");

                    return 1;
                }

                if (!argumentDict.ContainsKey("config"))
                {
                    Console.WriteLine("*** PackageBuilder: The config (-c) was not specified.");

                    return 1;
                }

                if (!argumentDict.ContainsKey("target"))
                {
                    Console.WriteLine("*** PackageBuilder: The target framework (-t) was not specified.");

                    return 1;
                }

                if (!argumentDict.ContainsKey("output"))
                {
                    Console.WriteLine("*** PackageBuilder: The output directory (-o) was not specified.");

                    return 1;
                }

                PackageBuilder.Create(argumentDict["spec"], argumentDict["config"], argumentDict["target"], argumentDict["output"]);

                return 0;
            }

            // settings
            var settingsFile = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, SettingsLoader.DefaultDirectory, argumentDict.TryGetValue("config", out var configArgument) ? configArgument : SettingsLoader.DefaultMainFile));

            if (!File.Exists(settingsFile))
            {
                Console.WriteLine($"The settings file '{settingsFile}' was not found. Usage: {Name} -config filename");

                return 1;
            }

            // initialization of the web server
            if (!OnInitialization(ArgumentParser.Current.GetValidArguments(args), settingsFile))
            {
                return 1;
            }

            // start the manager
            (_componentHub as ComponentHub).Execute();

            // starting the web server
            OnStart();

            // finish
            OnExit();

            return 0;
        }

        /// <summary>
        /// Called when the application is to be terminated using Ctrl+C.
        /// </summary>
        /// <param name="sender">The trigger of the event.</param>
        /// <param name="e">The event argument.</param>
        private void OnCancel(object sender, ConsoleCancelEventArgs e)
        {
            OnExit();
        }

        /// <summary>
        /// Initialization
        /// </summary>
        /// <param name="args">The valid arguments.</param>
        /// <param name="settingsFile">The main settings file; its directory is the settings directory.</param>
        /// <returns><see langword="true"/> when the server is ready to start, <see langword="false"/> when the settings could not be read.</returns>
        private bool OnInitialization(string args, string settingsFile)
        {
            var log = new Log();
            IConfigurationRoot configuration;

            // a broken settings file is the most likely reason for a failed start, so it is
            // reported by name and stops the start instead of surfacing as a stack trace
            try
            {
                configuration = SettingsLoader.Load(settingsFile, ex => log.Exception(ex));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"The settings could not be read: {ex.Message}");

                return false;
            }

            var settings = configuration.GetServerSettings();
            var settingsPath = Path.GetDirectoryName(settingsFile);
            var culture = CultureInfo.CurrentCulture;

            try
            {
                culture = new CultureInfo(settings.Culture);

                CultureInfo.CurrentCulture = culture;
            }
            catch
            {

            }

            var packageBase = ResolveDirectory(settings.PackagePath);
            var assetBase = ResolveDirectory(settings.AssetPath);
            var dataBase = ResolveDirectory(settings.DataPath);

            var context = new HttpServerContext
            (
                new RouteEndpoint(settings.ContextPath),
                settings.Endpoints,
                packageBase,
                assetBase,
                dataBase,
                settingsPath,
                configuration,
                culture,
                log,
                null
            );

            _httpServer = new HttpServer(context)
            {
                Settings = settings
            };

            _componentHub = ComponentActivator.CreateInstance<ComponentHub>(_httpServer.HttpServerContext);

            // apply the configured session lifetime once the manager exists; left unset, its
            // built-in bounded default stands
            if (settings.Session?.TimeoutMinutes is int timeoutMinutes && _componentHub.SessionManager is not null)
            {
                _componentHub.SessionManager.Timeout = TimeSpan.FromMinutes(timeoutMinutes);
            }

            // start logging
            _httpServer.HttpServerContext.Log?.Begin(settings.Log);

            // log program start
            _httpServer.HttpServerContext.Log?.Separator('/');
            _httpServer.HttpServerContext.Log?.Info(message: I18N.Translate("webexpress.webcore:app.startup"));
            _httpServer.HttpServerContext.Log?.Info(message: "".PadRight(80, '-'));
            _httpServer.HttpServerContext.Log?.Info(message: I18N.Translate("webexpress.webcore:app.version"), args: Version);
            _httpServer.HttpServerContext.Log?.Info(message: I18N.Translate("webexpress.webcore:app.arguments"), args: args);
            _httpServer.HttpServerContext.Log?.Info(message: I18N.Translate("webexpress.webcore:app.workingdirectory"), args: Environment.CurrentDirectory);
            _httpServer.HttpServerContext.Log?.Info(message: I18N.Translate("webexpress.webcore:app.packagebase"), args: packageBase);
            _httpServer.HttpServerContext.Log?.Info(message: I18N.Translate("webexpress.webcore:app.assetbase"), args: assetBase);
            _httpServer.HttpServerContext.Log?.Info(message: I18N.Translate("webexpress.webcore:app.database"), args: dataBase);
            _httpServer.HttpServerContext.Log?.Info(message: I18N.Translate("webexpress.webcore:app.settingsdirectory"), args: settingsPath);
            foreach (var file in configuration.Providers.OfType<SettingsDirectoryConfigurationProvider>().SelectMany(x => x.EnumerateFiles()))
            {
                _httpServer.HttpServerContext.Log?.Info(message: I18N.Translate("webexpress.webcore:app.settings"), args: Path.GetFileName(file));
            }
            _httpServer.HttpServerContext.Log?.Info(message: I18N.Translate("webexpress.webcore:app.logdirectory"), args: Path.GetDirectoryName(_httpServer.HttpServerContext.Log?.Filename));
            _httpServer.HttpServerContext.Log?.Info(message: I18N.Translate("webexpress.webcore:app.log"), args: Path.GetFileName(_httpServer.HttpServerContext.Log?.Filename));
            foreach (var v in settings.Endpoints)
            {
                _httpServer.HttpServerContext.Log?.Info(message: I18N.Translate("webexpress.webcore:app.uri"), args: v.Uri);
            }

            _httpServer.HttpServerContext.Log?.Separator('=');

            Directory.CreateDirectory(packageBase);
            Directory.CreateDirectory(assetBase);
            Directory.CreateDirectory(dataBase);

            Console.CancelKeyPress += OnCancel;

            Initialization?.Invoke(this, EventArgs.Empty);

            return true;
        }

        /// <summary>
        /// Turns a configured directory into an absolute one. A relative directory is taken
        /// relative to the working directory; an empty one is the working directory itself.
        /// </summary>
        /// <param name="directory">The configured directory.</param>
        /// <returns>The absolute directory.</returns>
        private static string ResolveDirectory(string directory)
        {
            return Path.GetFullPath(string.IsNullOrWhiteSpace(directory)
                ? Environment.CurrentDirectory
                : Path.Combine(Environment.CurrentDirectory, directory));
        }

        /// <summary>
        /// Initiates the HTTP server and raises the start event.
        /// </summary>
        private void OnStart()
        {
            _httpServer.Start();

            Start?.Invoke(this, EventArgs.Empty);

            Thread.CurrentThread.Join();
        }

        /// <summary>
        /// Performs cleanup operations when the application is exiting.
        /// </summary>
        private void OnExit()
        {
            _httpServer.Stop();

            Exit?.Invoke(this, EventArgs.Empty);

            // end of program log
            _httpServer.HttpServerContext.Log?.Separator('=');
            _httpServer.HttpServerContext.Log?.Info(message: I18N.Translate("webexpress.webcore:app.errors"), args: _httpServer.HttpServerContext.Log?.ErrorCount);
            _httpServer.HttpServerContext.Log?.Info(message: I18N.Translate("webexpress.webcore:app.warnings"), args: _httpServer.HttpServerContext.Log?.WarningCount);
            _httpServer.HttpServerContext.Log?.Info(message: I18N.Translate("webexpress.webcore:app.done"));
            _httpServer.HttpServerContext.Log?.Separator('/');

            // Stop running
            (_componentHub as ComponentHub).ShutDown();

            // stop logging
            _httpServer.HttpServerContext.Log?.Close();
        }

        /// <summary>
        /// Returns a component based on its id.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns>The instance of the component or null.</returns>
        public static IComponentManager GetComponent(string id)
        {
            return _componentHub?.GetComponentManager(id);
        }

        /// <summary>
        /// Returns a component based on its type.
        /// </summary>
        /// <typeparam name="T">The component class.</typeparam>
        /// <returns>The instance of the component or null.</returns>
        public static T GetComponent<T>() where T : IComponentManager
        {
            return _componentHub.GetComponentManager<T>();
        }
    }
}
