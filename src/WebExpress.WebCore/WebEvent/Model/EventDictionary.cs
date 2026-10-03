using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebEvent.Model
{
    /// <summary>
    /// Represents a dictionary that provides a mapping from plugin contexts to dictionaries of application contexts and event handler.
    /// </summary>
    internal class EventDictionary : Dictionary<IPluginContext, Dictionary<IApplicationContext, Dictionary<Type, IList<EventItem>>>>
    {
        /// <summary>
        /// Adds an event item to the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="eventItem">The event item.</param>
        /// <returns>True if the event handler item was added successfully, false if an element with the same key already exists.</returns>
        public bool AddEventItem(IPluginContext pluginContext, IApplicationContext applicationContext, EventItem eventItem)
        {
            var type = eventItem.EventClass;

            if (!typeof(IEvent).IsAssignableFrom(type))
            {
                return false;
            }

            if (!TryGetValue(pluginContext, out var appContextDict))
            {
                appContextDict = [];
                this[pluginContext] = appContextDict;
            }

            if (!appContextDict.TryGetValue(applicationContext, out var eventDict))
            {
                eventDict = [];
                appContextDict[applicationContext] = eventDict;
            }

            if (!eventDict.TryGetValue(type, out var eventList))
            {
                eventList = [];
                eventDict[type] = eventList;
            }

            if (eventList.Any(x => x.EventClass == type))
            {
                return false; // item with the same event handler already exists
            }

            eventList.Add(eventItem);

            return true;
        }

        /// <summary>
        /// Removes a event from the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContext">The application context.</param>
        public void RemoveEventHandler<TEvent>(IPluginContext pluginContext, IApplicationContext applicationContext)
            where TEvent : IEvent
        {
            var type = typeof(TEvent);

            if (ContainsKey(pluginContext))
            {
                var appContextDict = this[pluginContext];

                if (appContextDict.ContainsKey(applicationContext))
                {
                    var eventDict = appContextDict[applicationContext];

                    if (eventDict.ContainsKey(type))
                    {
                        eventDict.Remove(type);

                        if (eventDict.Count == 0)
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
        /// Returns the event handler from the dictionary.
        /// </summary>
        /// <typeparam name="TEvent">The type of event.</typeparam>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>An IEnumerable of event items</returns>
        public IEnumerable<EventItem> GetEventHandlerItems<TEvent>(IApplicationContext applicationContext)
            where TEvent : IEvent
        {
            return GetEventHandlerItems(applicationContext, typeof(TEvent));
        }

        /// <summary>
        /// Returns the event handler from the dictionary.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="eventType">The type of event.</param>
        /// <returns>An IEnumerable of event items</returns>
        public IEnumerable<EventItem> GetEventHandlerItems(IApplicationContext applicationContext, Type eventType)
        {
            if (!typeof(IEvent).IsAssignableFrom(eventType))
            {
                return [];
            }

            if (ContainsKey(applicationContext?.PluginContext))
            {
                var appContextDict = this[applicationContext?.PluginContext];

                if (appContextDict.ContainsKey(applicationContext))
                {
                    var eventDict = appContextDict[applicationContext];

                    if (eventDict.ContainsKey(eventType))
                    {
                        return eventDict[eventType];
                    }
                }
            }

            return [];
        }

        /// <summary>
        /// Returns all event handler contexts for a given plugin context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <returns>An IEnumerable of event handler contexts.</returns>
        public IEnumerable<IEventHandlerContext> GetEventHandlers(IPluginContext pluginContext)
        {
            return this.Where(entry => entry.Key == pluginContext)
                       .SelectMany(entry => entry.Value.Values)
                       .SelectMany(dict => dict.Values)
                       .SelectMany(x => x)
                       .Select(x => x.EventHandlerContext);
        }
    }
}
