using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebEvent;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebJob.Model
{
    /// <summary>
    /// Represents a dictionary that provides a mapping from plugin contexts to dictionaries of application contexts and jobs.
    /// key = plugin context
    /// value = scheduler items
    /// </summary>
    internal class ScheduleDictionary : Dictionary<IPluginContext, Dictionary<IApplicationContext, Dictionary<Type, IList<ScheduleItem>>>>
    {
        /// <summary>
        /// Adds a schedule item to the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="scheduleItem">The schedule item.</param>
        /// <returns>True if the schedule item was added successfully, false if an element with the same key already exists.</returns>
        public bool AddScheduleItem(IPluginContext pluginContext, IApplicationContext applicationContext, ScheduleItem scheduleItem)
        {
            var type = scheduleItem.JobClass;

            if (!typeof(IJob).IsAssignableFrom(type))
            {
                return false;
            }

            if (!ContainsKey(pluginContext))
            {
                this[pluginContext] = [];
            }

            var appContextDict = this[pluginContext];

            if (!appContextDict.ContainsKey(applicationContext))
            {
                appContextDict[applicationContext] = [];
            }

            var scheduleDict = appContextDict[applicationContext];

            if (!scheduleDict.ContainsKey(type))
            {
                scheduleDict[type] = [];
            }

            var scheduleList = scheduleDict[type];

            if (scheduleList.Any(x => x.JobClass == type))
            {
                return false; // item with the same event handler already exists
            }

            scheduleList.Add(scheduleItem);

            return true;
        }

        /// <summary>
        /// Removes a schedule item from the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContext">The application context.</param>
        public void RemoveScheduleItem<TEvent>(IPluginContext pluginContext, IApplicationContext applicationContext)
            where TEvent : IEvent
        {
            var type = typeof(TEvent);

            if (ContainsKey(pluginContext))
            {
                var appContextDict = this[pluginContext];

                if (appContextDict.ContainsKey(applicationContext))
                {
                    var scheduleDict = appContextDict[applicationContext];

                    if (scheduleDict.ContainsKey(type))
                    {
                        scheduleDict.Remove(type);

                        if (scheduleDict.Count == 0)
                        {
                            appContextDict.Remove(applicationContext);

                            if (appContextDict.Count == 0)
                            {
                                Remove(pluginContext);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns the schedule items from the dictionary.
        /// </summary>
        /// <typeparam name="TJob">The type of event.</typeparam>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>An IEnumerable of schedule items</returns>
        public IEnumerable<ScheduleItem> GetScheduleItems<TJob>(IApplicationContext applicationContext)
            where TJob : IJob
        {
            return GetScheduleItems(applicationContext, typeof(TJob));
        }

        /// <summary>
        /// Returns the schedule items from the dictionary.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="jobType">The type of job.</param>
        /// <returns>An IEnumerable of event items</returns>
        public IEnumerable<ScheduleItem> GetScheduleItems(IApplicationContext applicationContext, Type jobType)
        {
            if (!typeof(IEvent).IsAssignableFrom(jobType))
            {
                return [];
            }

            if (ContainsKey(applicationContext?.PluginContext))
            {
                var appContextDict = this[applicationContext?.PluginContext];

                if (appContextDict.ContainsKey(applicationContext))
                {
                    var scheduleDict = appContextDict[applicationContext];

                    if (scheduleDict.ContainsKey(jobType))
                    {
                        return scheduleDict[jobType];
                    }
                }
            }

            return [];
        }

        /// <summary>
        /// Returns all job contexts for a given plugin context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <returns>An IEnumerable of job contexts.</returns>
        public IEnumerable<IJobContext> GetJobs(IPluginContext pluginContext)
        {
            return this.Where(entry => entry.Key == pluginContext)
                       .SelectMany(entry => entry.Value.Values)
                       .SelectMany(dict => dict.Values)
                       .SelectMany(x => x)
                       .Select(x => x.JobContext);
        }
    }
}
