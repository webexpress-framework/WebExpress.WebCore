using System;

namespace WebExpress.WebCore.WebApplication
{
    /// <summary>
    /// Base class for a WebExpress application. Derive from it to create an application; the
    /// framework instantiates the subclass, supplies its <see cref="ApplicationContext"/> (id, name,
    /// paths, route), and calls <c>Run</c> at start-up. See <see cref="IApplication"/>.
    /// </summary>
    public abstract class Application : IApplication
    {
        /// <summary>
        /// Gets the context of the application.
        /// </summary>
        public IApplicationContext ApplicationContext { get; private set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Application()
        {
        }

        /// <summary>
        /// Initialization of the application. Here, for example, managed resources can be loaded. 
        /// </summary>
        /// <param name="applicationContext">The context that applies to the execution of the application</param>
        public virtual void Initialization(IApplicationContext applicationContext)
        {
            ApplicationContext = applicationContext;
        }

        /// <summary>
        /// Called when the application starts working. The call is concurrent. 
        /// </summary>
        public virtual void Run()
        {
        }

        /// <summary>
        /// Release unmanaged resources that have been reserved during use.
        /// </summary>
        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
