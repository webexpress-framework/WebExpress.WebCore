using System;
using System.Threading;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebJob.Model
{
    /// <summary>
    /// Represents an appointment entry in the appointment execution directory.
    /// </summary>
    internal class ScheduleItem : IDisposable
    {
        private int _disposed;

        /// <summary>
        /// Gets the associated plugin context.
        /// </summary>
        public IPluginContext PluginContext { get; internal set; }

        /// <summary>
        /// Gets the corresponding application context.
        /// </summary>
        public IApplicationContext ApplicationContext { get; internal set; }

        /// <summary>
        /// Gets the context associated with the job.
        /// </summary>
        public IJobContext JobContext { get; internal set; }

        /// <summary>
        /// Gets the job class.
        /// </summary>
        public Type JobClass { get; internal set; }

        /// <summary>
        /// Gets the job instance.
        /// </summary>
        public IJob Instance { get; internal set; }

        /// <summary>
        /// Gets the cancel token or null if not already created.
        /// </summary>
        public CancellationTokenSource TokenSource { get; } = new CancellationTokenSource();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The associated component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        /// <param name="pluginContext">The associated plugin context.</param>
        /// <param name="applicationContext">The corresponding application context.</param>
        /// <param name="jobContext">The job context.</param>
        /// <param name="jobClass">The job class.</param>
        public ScheduleItem(IComponentHub componentHub, IHttpServerContext httpServerContext, IPluginContext pluginContext, IApplicationContext applicationContext, IJobContext jobContext, Type jobClass)
        {
            PluginContext = pluginContext;
            ApplicationContext = applicationContext;
            JobContext = jobContext;
            JobClass = jobClass;

            Instance = ComponentActivator.CreateInstance<IJob, IJobContext>
            (
                jobClass,
                jobContext,
                httpServerContext,
                componentHub,
                pluginContext,
                applicationContext
            );

            return;
        }

        /// <summary>
        /// Determines whether the job has been released. A run that was already scheduled when
        /// the job was removed checks this so it does not process a disposed instance.
        /// </summary>
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        /// <summary>
        /// Releases the job instance as soon as the job is removed, so a plugin that is unloaded
        /// at runtime leaves no job holding handles or references into its load context. Both the
        /// removal and the shutdown of the manager may reach the same item, so only the first
        /// call has an effect.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                TokenSource.Cancel();
                Instance?.Dispose();
            }
            finally
            {
                TokenSource.Dispose();
            }
        }

        /// <summary>
        /// Convert the resource element to a string.
        /// </summary>
        /// <returns>The resource element in its string representation.</returns>
        public override string ToString()
        {
            return "Job ${Id}";
        }
    }
}
