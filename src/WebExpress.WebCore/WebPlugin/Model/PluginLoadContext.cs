using System.Reflection;
using System.Runtime.Loader;

namespace WebExpress.WebCore.WebPlugin.Model
{
    /// <summary>
    /// Isolation of plug-in dependencies.
    /// see https://github.com/dotnet/samples/tree/main/core/extensions/AppWithPlugin
    /// </summary>
    public class PluginLoadContext : AssemblyLoadContext
    {
        /// <summary>
        /// Gets or set the resolver to resolve dependencies.
        /// </summary>
        private AssemblyDependencyResolver Resolver { get; set; }

        /// <summary>
        /// Initializes a new instance of the class. The context is collectible, because a plugin
        /// removed at runtime unloads it; a non-collectible context refuses to unload and would
        /// keep the plugin's assemblies in the process until it ends.
        /// </summary>
        /// <param name="pluginPath">The base path of the plugin.</param>
        public PluginLoadContext(string pluginPath)
            : base(isCollectible: true)
        {
            Resolver = new AssemblyDependencyResolver(pluginPath);
        }

        /// <summary>
        /// Allows an assembly to be resolved based on its AssemblyName.
        /// </summary>
        /// <param name="assemblyName">The object that describes the assembly to be resolved.</param>
        /// <returns>The resolved assembly, or null.</returns>
        protected override Assembly Load(AssemblyName assemblyName)
        {
            string assemblyPath = Resolver.ResolveAssemblyToPath(assemblyName);

            if (assemblyPath is not null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        /// <summary>
        /// Allows to load an unmanaged library by name.
        /// </summary>
        /// <param name="unmanagedDllName">Name of the unmanaged library. Typically this is the filename without its path or extensions.</param>
        /// <returns>A handle to the loaded library, or Zero.</returns>
        protected override nint LoadUnmanagedDll(string unmanagedDllName)
        {
            string libraryPath = Resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

            if (libraryPath is not null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return nint.Zero;
        }
    }
}
