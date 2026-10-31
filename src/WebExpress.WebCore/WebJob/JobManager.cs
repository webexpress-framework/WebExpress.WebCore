using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebCluster;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebJob.Model;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebJob
{
    /// <summary>
    /// Central registry and scheduler for jobs — recurring background tasks that run on a schedule
    /// (similar to cron). It discovers the jobs a plugin provides, keeps track of them, and runs
    /// them at their due times.
    /// </summary>
    /// <remarks>
    /// This class manages the processing of cyclic jobs. It provides methods to register, remove, and execute jobs.
    /// </remarks>
    public sealed class JobManager : IJobManager, ISystemComponent, IExecutableElements
    {
        /// <summary>
        /// The scope job claims are kept under in the cluster store.
        /// </summary>
        internal const string ClaimScope = "job";

        // a claim must outlive the clock skew between instances; an hour is far beyond any skew
        // a cluster would tolerate and still keeps the claims few
        private static readonly TimeSpan ClaimLifetime = TimeSpan.FromHours(1);

        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly ScheduleDictionary _staticScheduleDictionary = [];
        private readonly List<ScheduleItem> _dynamicScheduleList = [];
        private readonly CancellationTokenSource _tokenSource = new();
        private readonly Clock _clock = new();

        /// <summary>
        /// An event that fires when an job is added.
        /// </summary>
        public event EventHandler<IJobContext> AddJob;

        /// <summary>
        /// An event that fires when an job is removed.
        /// </summary>
        public event EventHandler<IJobContext> RemoveJob;

        /// <summary>
        /// Gets all job contextes.
        /// </summary>
        public IEnumerable<IJobContext> Jobs => _staticScheduleDictionary
            .SelectMany(x => x.Value)
            .SelectMany(x => x.Value)
            .SelectMany(x => x.Value)
            .Select(x => x.JobContext)
            .Union(_dynamicScheduleList.Select(x => x.JobContext));

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private JobManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;

            _componentHub?.PluginManager?.AddPlugin += OnAddPlugin;
            _componentHub?.PluginManager?.RemovePlugin += OnRemovePlugin;
            _componentHub?.ApplicationManager.AddApplication += OnAddApplication;
            _componentHub?.ApplicationManager.RemoveApplication += OnRemoveApplication;
            _httpServerContext = httpServerContext;

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate
                (
                    "webexpress.webcore:jobmanager.initialization"
                )
            );
        }

        /// <summary>
        /// Discovers and binds static jobs to an application.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin whose jobs are to be associated.</param>
        private void Register(IPluginContext pluginContext)
        {
            if (_staticScheduleDictionary.ContainsKey(pluginContext))
            {
                return;
            }

            Register(pluginContext, _componentHub?.ApplicationManager.GetApplications(pluginContext));
        }

        /// <summary>
        /// Discovers and binds jobs to an application.
        /// </summary>
        /// <param name="applicationContext">The context of the application whose jobs are to be associated.</param>
        private void Register(IApplicationContext applicationContext)
        {
            foreach (var pluginContext in _componentHub?.PluginManager?.GetPlugins(applicationContext))
            {
                if (_staticScheduleDictionary.TryGetValue(pluginContext, out var appDict) && appDict.ContainsKey(applicationContext))
                {
                    continue;
                }

                Register(pluginContext, [applicationContext]);
            }
        }

        /// <summary>
        /// Registers resources for a given plugin and application context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContexts">The application context (optional).</param>
        private void Register(IPluginContext pluginContext, IEnumerable<IApplicationContext> applicationContexts)
        {
            var assembly = pluginContext?.Assembly;

            foreach (var job in assembly.GetTypes().Where
                (
                    x => x.IsClass == true &&
                    x.IsSealed &&
                    x.IsPublic &&
                    x.GetInterface(typeof(IJob).Name) is not null
                ))
            {
                var id = job.FullName?.ToLower();

                var minute = "*";
                var hour = "*";
                var day = "*";
                var month = "*";
                var weekday = "*";
                var name = default(string);
                var description = default(string);
                var scope = JobScope.Cluster;

                foreach (var customAttribute in job.CustomAttributes
                    .Where(x => x.AttributeType.GetInterfaces().Contains(typeof(IJobAttribute))))
                {
                    if (customAttribute.AttributeType == typeof(JobAttribute))
                    {
                        minute = customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                        hour = customAttribute.ConstructorArguments.Skip(1).FirstOrDefault().Value?.ToString();
                        day = customAttribute.ConstructorArguments.Skip(2).FirstOrDefault().Value?.ToString();
                        month = customAttribute.ConstructorArguments.Skip(3).FirstOrDefault().Value?.ToString();
                        weekday = customAttribute.ConstructorArguments.Skip(4).FirstOrDefault().Value?.ToString();
                    }
                    else if (customAttribute.AttributeType == typeof(NameAttribute))
                    {
                        name = customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    }
                    else if (customAttribute.AttributeType == typeof(DescriptionAttribute))
                    {
                        description = customAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    }
                    else if (customAttribute.AttributeType == typeof(JobScopeAttribute)
                        && customAttribute.ConstructorArguments.FirstOrDefault().Value is int value)
                    {
                        scope = (JobScope)value;
                    }
                }

                // assign the job to existing applications
                foreach (var applicationContext in applicationContexts)
                {
                    var jobContext = new JobContext()
                    {
                        JobId = new ComponentId(job.FullName),
                        JobName = name,
                        Description = description,
                        PluginContext = pluginContext,
                        ApplicationContext = applicationContext,
                        Cron = new Cron(_httpServerContext, minute, hour, day, month, weekday),
                        Scope = scope
                    };

                    if (job != default)
                    {
                        if (_staticScheduleDictionary.AddScheduleItem
                        (
                            pluginContext,
                            applicationContext,
                            new ScheduleItem(_componentHub, _httpServerContext, pluginContext, applicationContext, jobContext, job)
                        ))
                        {
                            OnAddJob(jobContext);

                            _httpServerContext?.Log?.Debug
                            (
                                I18N.Translate
                                (
                                    "webexpress.webcore:jobmanager.register",
                                    id,
                                    applicationContext.ApplicationId
                                )
                            );
                        }
                        else
                        {
                            _httpServerContext?.Log?.Debug
                            (
                                I18N.Translate
                                (
                                    "webexpress.webcore:jobmanager.duplicate",
                                    id,
                                    applicationContext.ApplicationId
                                )
                            );
                        }
                    }
                    else
                    {
                        _httpServerContext?.Log?.Debug
                        (
                            I18N.Translate
                            (
                                "webexpress.webcore:jobmanager.jobless",
                                id
                            )
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Removes all jobs associated with the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The context of the plugin that contains the jobs to remove.</param>
        internal void Remove(IPluginContext pluginContext)
        {
            if (pluginContext is null)
            {
                return;
            }

            // the plugin has not been registered in the manager
            if (_staticScheduleDictionary.TryGetValue(pluginContext, out var value))
            {
                var scheduleItems = value.Values
                    .SelectMany(x => x.Values)
                    .SelectMany(x => x)
                    .ToList();

                _staticScheduleDictionary.Remove(pluginContext);

                foreach (var scheduleItem in scheduleItems)
                {
                    OnRemoveJob(scheduleItem.JobContext);
                    Release(scheduleItem);
                }
            }
        }

        /// <summary>
        /// Removes all jobs associated with the specified application context.
        /// </summary>
        /// <param name="applicationContext">The context of the application that contains the jobs to remove.</param>
        internal void Remove(IApplicationContext applicationContext)
        {
            if (applicationContext is null)
            {
                return;
            }

            var scheduleItems = new List<ScheduleItem>();

            foreach (var pluginDict in _staticScheduleDictionary.Values)
            {
                if (pluginDict.Remove(applicationContext, out var appDict))
                {
                    scheduleItems.AddRange(appDict.Values.SelectMany(x => x));
                }
            }

            foreach (var scheduleItem in scheduleItems)
            {
                OnRemoveJob(scheduleItem.JobContext);
                Release(scheduleItem);
            }
        }

        /// <summary>
        /// Removes a dynamic job.
        /// </summary>
        /// <param name="job">The job to remove.</param>
        public void Remove(IJob job)
        {
            var scheduleItems = _dynamicScheduleList
                .Where(x => x.Instance == job)
                .ToList();

            _dynamicScheduleList.RemoveAll(scheduleItems.Contains);

            foreach (var scheduleItem in scheduleItems)
            {
                OnRemoveJob(scheduleItem.JobContext);
                Release(scheduleItem);
            }
        }

        /// <summary>
        /// Disposes a removed job. A job that fails to release its resources must not keep
        /// the remaining jobs - or the other managers listening to the same plugin removal -
        /// from being cleaned up.
        /// </summary>
        /// <param name="scheduleItem">The schedule entry of the removed job.</param>
        private void Release(ScheduleItem scheduleItem)
        {
            try
            {
                scheduleItem.Dispose();
            }
            catch (Exception ex)
            {
                _httpServerContext?.Log?.Exception(ex);
            }
        }

        /// <summary>
        /// Raises the AddJob event.
        /// </summary>
        /// <param name="jobContext">The job context.</param>
        private void OnAddJob(IJobContext jobContext)
        {
            AddJob?.Invoke(this, jobContext);
        }

        /// <summary>
        /// Raises the RemoveJob event.
        /// </summary>
        /// <param name="jobContext">The job context.</param>
        private void OnRemoveJob(IJobContext jobContext)
        {
            RemoveJob?.Invoke(this, jobContext);
        }

        /// <summary>
        /// Raises the event when an plugin is added.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The context of the plugin being added.</param>
        private void OnAddPlugin(object sender, IPluginContext e)
        {
            Register(e);
        }

        /// <summary>  
        /// Raises the event when a plugin is removed.  
        /// </summary>  
        /// <param name="sender">The source of the event.</param>  
        /// <param name="e">The context of the plugin being removed.</param>  
        private void OnRemovePlugin(object sender, IPluginContext e)
        {
            Remove(e);
        }

        /// <summary>
        /// Raises the event when an application is removed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The context of the application being removed.</param>
        private void OnRemoveApplication(object sender, IApplicationContext e)
        {
            Remove(e);
        }

        /// <summary>
        /// Raises the event when an application is added.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The context of the application being added.</param>
        private void OnAddApplication(object sender, IApplicationContext e)
        {
            Register(e);
        }

        /// <summary>
        /// Executes the schedule.
        /// </summary>
        internal void Execute()
        {
            _httpServerContext.Lifetime.TryRun(async stopping =>
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(stopping, _tokenSource.Token);
                while (!linked.IsCancellationRequested)
                {
                    Update();

                    var secondsLeft = 60 - DateTime.Now.Second;
                    await Task.Delay(TimeSpan.FromSeconds(secondsLeft), linked.Token).ConfigureAwait(false);
                }

            });
        }

        /// <summary>
        /// Run jobs on demand (concurrent execution).
        /// </summary>
        private void Update()
        {
            foreach (var clock in _clock.Synchronize())
            {
                foreach (var scheduleItemValue in _staticScheduleDictionary
                    .SelectMany(x => x.Value)
                    .SelectMany(x => x.Value)
                    .SelectMany(x => x.Value)
                    .Union(_dynamicScheduleList.Select(x => x)))
                {
                    if (scheduleItemValue.JobContext.Cron.Matching(_clock) && Claim(scheduleItemValue.JobContext, _clock))
                    {
                        _httpServerContext?.Log?.Debug
                        (
                            I18N.Translate
                            (
                                "webexpress.webcore:jobmanager.job.process",
                                scheduleItemValue.JobContext.JobId
                            )
                        );

                        _httpServerContext.Lifetime.TryRun(() =>
                        {
                            // the job may have been removed between scheduling and running
                            if (scheduleItemValue.IsDisposed)
                            {
                                return;
                            }

                            scheduleItemValue.Instance?.Process();
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Claims a due run of a job for this instance. Every instance evaluates the same cron
        /// expression at the same minute; the atomic add in the shared store lets exactly one of
        /// them win, so a cluster runs the job once instead of once per instance.
        /// </summary>
        /// <param name="jobContext">The job that is due.</param>
        /// <param name="clock">The minute the job is due at.</param>
        /// <returns>True when this instance runs the job.</returns>
        internal bool Claim(IJobContext jobContext, Clock clock)
        {
            var cluster = _componentHub?.ClusterManager;

            if (jobContext.Scope == JobScope.Node || cluster?.Store is not { IsShared: true } store)
            {
                return true;
            }

            // utc, so instances whose containers run in different time zones name the same run alike
            var key = string.Join("|", jobContext.ApplicationContext?.ApplicationId, jobContext.JobId,
                clock.Moment.ToUniversalTime().ToString("yyyyMMddHHmm", System.Globalization.CultureInfo.InvariantCulture));

            try
            {
                if (store.TryAdd(ClaimScope, key, System.Text.Encoding.UTF8.GetBytes(cluster.NodeId), ClaimLifetime))
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
            {
                // without the store no instance can claim; skipping one run beats running it everywhere
                _httpServerContext?.Log?.Exception(ex);

                return false;
            }

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate("webexpress.webcore:jobmanager.job.claimed", jobContext.JobId)
            );

            return false;
        }

        /// <summary>
        /// Returns a JobContext instance associated with an application.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="jobType">The type of the job.</param>
        /// <returns>A JobContext instance.</returns>
        public IJobContext GetJob(IApplicationContext applicationContext, Type jobType)
        {
            return _staticScheduleDictionary
                .SelectMany(x => x.Value)
                .SelectMany(x => x.Value)
                .SelectMany(x => x.Value)
                .Union(_dynamicScheduleList.Select(x => x))
                .FirstOrDefault(x => x.JobContext.ApplicationContext == applicationContext && x.JobClass == jobType)?.JobContext;
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            _componentHub?.PluginManager?.AddPlugin -= OnAddPlugin;
            _componentHub?.PluginManager?.RemovePlugin -= OnRemovePlugin;
            _componentHub?.ApplicationManager.AddApplication -= OnAddApplication;
            _componentHub?.ApplicationManager.RemoveApplication -= OnRemoveApplication;

            _tokenSource.Cancel();

            foreach (var item in _staticScheduleDictionary.Values.SelectMany(x => x.Values)
                .SelectMany(x => x.Values).SelectMany(x => x).Concat(_dynamicScheduleList).Distinct())
            {
                Release(item);
            }

            _staticScheduleDictionary.Clear();
            _dynamicScheduleList.Clear();
        }
    }
}
