using System.Reflection;
using System.Reflection.Emit;
using WebExpress.WebCore.Test.Data;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebHealth;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.Test.Fixture
{
    /// <summary>
    /// Supplies isolated plugin assemblies and dependency behavior while exercising real component discovery.
    /// </summary>
    internal static class HealthTestFixture
    {
        /// <summary>
        /// Binds a configurable test component through the same discovery method used by lifecycle events.
        /// </summary>
        /// <param name="manager">The health manager under test.</param>
        /// <param name="application">The application owning the test dependency.</param>
        /// <param name="name">The diagnostic identifier of the isolated contributing plugin.</param>
        /// <param name="check">The behavior executed by the discovered test component.</param>
        /// <param name="timeout">An optional short budget for testing timeout behavior.</param>
        /// <returns>The plugin binding whose disposal simulates plugin removal.</returns>
        internal static HealthTestRegistration Register(IHealthManager manager, IApplicationContext application,
            string name, Func<CancellationToken, Task<HealthCheckResult>> check, TimeSpan? timeout = null)
        {
            var plugin = new HealthTestPluginContext(name, typeof(TestHealth)) { Check = check };
            ((HealthManager)manager).Register(plugin, [application]);
            var context = manager.HealthChecks.SingleOrDefault(x => x.PluginContext == plugin);
            if (timeout.HasValue && context is HealthContext metadata)
            {
                metadata.Timeout = timeout.Value;
            }

            return new HealthTestRegistration((HealthManager)manager, plugin);
        }

        /// <summary>
        /// Emits a public sealed application whose constructor throws.
        /// </summary>
        /// <remarks>
        /// The application manager only discovers public top-level types, and such a type in the
        /// test assembly would be registered - and fail - in every test that loads the test plugin.
        /// A type in its own dynamic assembly is visible only to the plugin context it is handed to.
        /// </remarks>
        /// <param name="message">The message of the exception the constructor throws.</param>
        /// <returns>The emitted application type.</returns>
        internal static Type CreateThrowingApplication(string message)
        {
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("WebExpress.WebCore.Test.ThrowingApplication"),
                AssemblyBuilderAccess.RunAndCollect);
            var type = assembly.DefineDynamicModule("ThrowingApplication").DefineType("WebExpress.WebCore.Test.ThrowingApplication",
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class, typeof(object), [typeof(IApplication), typeof(IDisposable)]);

            var constructor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes).GetILGenerator();
            constructor.Emit(OpCodes.Ldstr, message);
            constructor.Emit(OpCodes.Newobj, typeof(InvalidOperationException).GetConstructor([typeof(string)]));
            constructor.Emit(OpCodes.Throw);

            foreach (var declaration in new[] { typeof(IApplication).GetMethod(nameof(IApplication.Run)), typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose)) })
            {
                var method = type.DefineMethod(declaration.Name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final |
                    MethodAttributes.HideBySig | MethodAttributes.NewSlot, typeof(void), Type.EmptyTypes);
                method.GetILGenerator().Emit(OpCodes.Ret);
                type.DefineMethodOverride(method, declaration);
            }

            return type.CreateType();
        }
    }

    /// <summary>
    /// Associates controllable dependency behavior with an otherwise normal plugin context.
    /// </summary>
    internal sealed class HealthTestPluginContext : PluginContext
    {
        /// <summary>
        /// Gets or sets the behavior executed by the test component.
        /// </summary>
        internal Func<CancellationToken, Task<HealthCheckResult>> Check { get; set; } =
            _ => Task.FromResult(HealthCheckResult.Healthy());

        /// <summary>
        /// Gets or sets whether component construction must fail before a check can execute.
        /// </summary>
        internal bool FailConstruction { get; set; }

        /// <summary>
        /// Gets or sets the number of component instances created for this plugin.
        /// </summary>
        internal int Created { get; set; }

        /// <summary>
        /// Gets or sets the number of component instances disposed for this plugin.
        /// </summary>
        internal int Disposed { get; set; }

        /// <summary>
        /// Gets the injected contexts recorded when each application binding is activated.
        /// </summary>
        internal List<IHealthContext> ActivatedContexts { get; } = [];

        /// <summary>
        /// Signals that a component was released after its active check finished.
        /// </summary>
        internal TaskCompletionSource DisposedSignal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Restricts discovery to explicit fixture types without loading another physical assembly.
        /// </summary>
        /// <param name="name">The unique plugin identifier used by the test.</param>
        /// <param name="types">The component types exposed by this isolated assembly.</param>
        internal HealthTestPluginContext(string name, params Type[] types)
        {
            PluginId = new ComponentId(name);
            Assembly = new HealthTestAssembly(types);
        }
    }

    /// <summary>
    /// Exposes a controlled type inventory to the real discovery pipeline.
    /// </summary>
    internal sealed class HealthTestAssembly : Assembly
    {
        private readonly Type[] _types;

        /// <summary>
        /// Keeps unrelated test components out of an isolated discovery scenario.
        /// </summary>
        /// <param name="types">The types visible to the plugin's discovery operation.</param>
        internal HealthTestAssembly(Type[] types)
        {
            _types = types;
        }

        /// <summary>
        /// Provides the declared fixture inventory to component discovery.
        /// </summary>
        /// <returns>The types supplied by the test.</returns>
        public override Type[] GetTypes()
        {
            return _types;
        }

        /// <summary>
        /// Provides the declared fixture inventory to application discovery, which reads exported types only.
        /// </summary>
        /// <returns>The types supplied by the test.</returns>
        public override Type[] GetExportedTypes()
        {
            return _types;
        }
    }

    /// <summary>
    /// Simulates removal of an isolated contributing plugin without modifying other bindings.
    /// </summary>
    internal sealed class HealthTestRegistration : IDisposable
    {
        private readonly HealthManager _manager;

        /// <summary>
        /// Gets the plugin state used to inspect activation and disposal.
        /// </summary>
        internal HealthTestPluginContext Plugin { get; }

        /// <summary>
        /// Associates cleanup with the manager that owns the test plugin's bindings.
        /// </summary>
        /// <param name="manager">The manager owning the bindings.</param>
        /// <param name="plugin">The isolated plugin removed during cleanup.</param>
        internal HealthTestRegistration(HealthManager manager, HealthTestPluginContext plugin)
        {
            _manager = manager;
            Plugin = plugin;
        }

        /// <summary>
        /// Runs the manager's normal plugin removal path.
        /// </summary>
        public void Dispose()
        {
            _manager.Remove(Plugin);
        }
    }
}
