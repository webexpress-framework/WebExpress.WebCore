using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.WebMetrics.Model
{
    /// <summary>
    /// Records what the framework itself observes - requests, logins, users, process resources - so
    /// that every application gets the operational baseline without writing a component of its own.
    /// </summary>
    /// <remarks>
    /// The process and runtime series use the names of the official Prometheus client libraries,
    /// so existing dashboards and alert rules apply unchanged.
    /// </remarks>
    internal sealed class FrameworkMetrics
    {
        private static readonly string _version = typeof(FrameworkMetrics).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(FrameworkMetrics).Assembly.GetName().Version?.ToString() ?? string.Empty;

        private readonly IComponentHub _componentHub;
        private readonly ConcurrentDictionary<Guid, long> _activeUsers = new();

        /// <summary>
        /// Gets the handled requests by method and status code, from which error rates are derived.
        /// </summary>
        internal MetricCounter Requests { get; } = new("webexpress_http_requests_total",
            "Handled HTTP requests by method and status code, excluding health and metrics probes.", "method", "code");

        /// <summary>
        /// Gets the time from receiving a request to handing its response to the transport.
        /// </summary>
        internal MetricHistogram RequestDuration { get; } = new("webexpress_http_request_duration_seconds",
            "Time from receiving a request until its response is ready to be sent.", null);

        /// <summary>
        /// Gets the requests currently being processed, which reveals saturation before latency does.
        /// </summary>
        internal MetricGauge RequestsInFlight { get; } = new("webexpress_http_requests_in_flight",
            "HTTP requests currently being processed, excluding open WebSocket connections.");

        /// <summary>
        /// Gets the login attempts by outcome, so brute force attempts and broken identity providers show up as rates.
        /// </summary>
        internal MetricCounter Logins { get; } = new("webexpress_identity_logins_total",
            "Login attempts by result (success, failure, throttled).", "result");

        /// <summary>
        /// Gets the explicit sign-outs.
        /// </summary>
        internal MetricCounter Logouts { get; } = new("webexpress_identity_logouts_total",
            "Explicit sign-outs.");

        /// <summary>
        /// Gets the instruments the framework records into, which the manager exports like any other.
        /// </summary>
        internal MetricInstrument[] Instruments => [Requests, RequestDuration, RequestsInFlight, Logins, Logouts];

        /// <summary>
        /// Gets or sets the period within which an authenticated request makes a user count as active.
        /// </summary>
        internal TimeSpan ActiveUserWindow { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Binds the framework series to the hub whose managers they describe.
        /// </summary>
        /// <param name="componentHub">The hub supplying plugin, application and session state.</param>
        internal FrameworkMetrics(IComponentHub componentHub)
        {
            _componentHub = componentHub;
        }

        /// <summary>
        /// Notes that an identity made an authenticated request. Called once per request, so it only
        /// stores a timestamp and leaves the counting to the scrape.
        /// </summary>
        /// <param name="identityId">The id of the authenticated identity.</param>
        internal void RecordActiveUser(Guid identityId)
        {
            _activeUsers[identityId] = Environment.TickCount64;
        }

        /// <summary>
        /// Records a handled request.
        /// </summary>
        /// <param name="method">The request method, or null when the request could not be parsed.</param>
        /// <param name="status">The status code of the response.</param>
        /// <param name="duration">The processing time.</param>
        internal void RecordRequest(string method, int status, TimeSpan duration)
        {
            Requests.Increment(method ?? "UNKNOWN", status.ToString(System.Globalization.CultureInfo.InvariantCulture));
            RequestDuration.Observe(Math.Max(0, duration.TotalSeconds));
        }

        /// <summary>
        /// Reports the framework series read at scrape time; the instruments are exported by the manager.
        /// </summary>
        /// <param name="collector">The collector receiving the values.</param>
        internal void Collect(IMetricCollector collector)
        {
            collector.Gauge("webexpress_info", "Version of the WebExpress framework.", 1, new MetricLabel("version", _version));
            collector.Gauge("webexpress_identity_active_users",
                $"Distinct identities with an authenticated request within the last {ActiveUserWindow.TotalMinutes:0.##} minutes.",
                CountActiveUsers());
            collector.Gauge("webexpress_sessions", "Sessions held by the server, including idle ones not yet cleaned up.",
                _componentHub.SessionManager?.Count ?? 0);
            collector.Gauge("webexpress_plugins", "Loaded plugins.", _componentHub.PluginManager?.Plugins.Count() ?? 0);
            collector.Gauge("webexpress_applications", "Registered applications.", _componentHub.ApplicationManager?.Applications.Count() ?? 0);
            collector.Gauge("webexpress_applications_failed", "Declared applications whose creation failed.",
                _componentHub.ApplicationManager?.FailedApplications.Count() ?? 0);

            foreach (var (node, skew) in _componentHub.ClusterManager?.ClockSkew ?? new Dictionary<string, TimeSpan>())
            {
                collector.Gauge("webexpress_cluster_clock_skew_seconds",
                    "How far the clock of another cluster instance ran ahead of this one on its last message.",
                    skew.TotalSeconds, new MetricLabel("peer", node));
            }

            CollectProcess(collector);
            CollectRuntime(collector);
        }

        /// <summary>
        /// Counts the identities seen within the window and forgets older ones, which keeps the
        /// store bounded by the number of recently active users.
        /// </summary>
        /// <returns>The number of active users.</returns>
        private int CountActiveUsers()
        {
            var threshold = Environment.TickCount64 - (long)ActiveUserWindow.TotalMilliseconds;
            var count = 0;

            foreach (var (id, seen) in _activeUsers)
            {
                if (seen >= threshold)
                {
                    count++;
                }
                else
                {
                    // removes the entry only if it was not refreshed in the meantime
                    _activeUsers.TryRemove(new(id, seen));
                }
            }

            return count;
        }

        /// <summary>
        /// Reports the resources the operating system accounts to the process, which is what a
        /// container limit is enforced against.
        /// </summary>
        /// <param name="collector">The collector receiving the values.</param>
        private static void CollectProcess(IMetricCollector collector)
        {
            using var process = Process.GetCurrentProcess();

            collector.Counter("process_cpu_seconds_total", "Total user and system CPU time spent in seconds.",
                process.TotalProcessorTime.TotalSeconds);
            collector.Gauge("process_resident_memory_bytes", "Resident memory size in bytes.", process.WorkingSet64);
            collector.Gauge("process_virtual_memory_bytes", "Virtual memory size in bytes.", process.VirtualMemorySize64);
            collector.Gauge("process_private_memory_bytes", "Private memory size in bytes.", process.PrivateMemorySize64);
            collector.Gauge("process_open_handles", "Number of open handles.", process.HandleCount);
            collector.Gauge("process_num_threads", "Number of operating system threads.", process.Threads.Count);
            collector.Gauge("process_start_time_seconds", "Start time of the process since unix epoch in seconds.",
                new DateTimeOffset(process.StartTime).ToUnixTimeMilliseconds() / 1000.0);
        }

        /// <summary>
        /// Reports the managed runtime, which explains memory growth and latency spikes that the
        /// process series only show.
        /// </summary>
        /// <param name="collector">The collector receiving the values.</param>
        private static void CollectRuntime(IMetricCollector collector)
        {
            collector.Gauge("dotnet_total_memory_bytes", "Bytes currently allocated on the managed heap.", GC.GetTotalMemory(false));
            collector.Gauge("dotnet_gc_heap_size_bytes", "Size of the managed heap after the last garbage collection.",
                GC.GetGCMemoryInfo().HeapSizeBytes);
            collector.Counter("dotnet_gc_pause_seconds_total", "Time the runtime was paused for garbage collection.",
                GC.GetTotalPauseDuration().TotalSeconds);

            for (var generation = 0; generation <= GC.MaxGeneration; generation++)
            {
                collector.Counter("dotnet_collection_count_total", "Garbage collections by generation.",
                    GC.CollectionCount(generation), new MetricLabel("generation", generation.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }

            collector.Gauge("dotnet_threadpool_threads", "Threads in the thread pool.", ThreadPool.ThreadCount);
            collector.Gauge("dotnet_threadpool_queue_length", "Work items waiting for a thread pool thread.", ThreadPool.PendingWorkItemCount);
            collector.Counter("dotnet_threadpool_completed_items_total", "Work items completed by the thread pool.",
                ThreadPool.CompletedWorkItemCount);
        }
    }
}
