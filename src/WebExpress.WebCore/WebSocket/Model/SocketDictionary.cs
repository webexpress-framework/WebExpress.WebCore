using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebSocket.Model
{
    /// <summary>
    /// Represents a dictionary that maps plugin contexts to application contexts, socket types, and socket items.
    /// key = plugin context
    /// value = application context { key = socket type, value = socket item }
    /// </summary>
    internal class SocketDictionary
    {
        private readonly Dictionary<IPluginContext, Dictionary<IApplicationContext, Dictionary<Type, SocketItem>>> _dict = new Dictionary<IPluginContext, Dictionary<IApplicationContext, Dictionary<Type, SocketItem>>>();

        /// <summary>
        /// Gets all socket contexts.
        /// </summary>
        public IEnumerable<ISocketContext> All => _dict.Values
            .SelectMany(x => x.Values)
            .SelectMany(x => x.Values)
            .Select(x => x.SocketContext);

        /// <summary>
        /// Adds a socket item to the dictionary.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="socketItem">The socket item.</param>
        /// <returns>
        /// True if the socket item was added successfully, false if an element with the same key already exists.
        /// </returns>
        public bool AddSocketItem(IPluginContext pluginContext, IApplicationContext applicationContext, SocketItem socketItem)
        {
            var type = socketItem.SocketClass;

            if (type.GetInterface(typeof(ISocket).Name) is null)
            {
                return false;
            }

            if (!_dict.TryGetValue(pluginContext, out Dictionary<IApplicationContext, Dictionary<Type, SocketItem>> appContextDict))
            {
                appContextDict = new Dictionary<IApplicationContext, Dictionary<Type, SocketItem>>();
                _dict[pluginContext] = appContextDict;
            }

            if (!appContextDict.TryGetValue(applicationContext, out Dictionary<Type, SocketItem> socketDict))
            {
                socketDict = new Dictionary<Type, SocketItem>();
                appContextDict[applicationContext] = socketDict;
            }

            if (!socketDict.ContainsKey(type))
            {
                socketDict[type] = socketItem;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes all socket items from the dictionary for the given plugin context.
        /// </summary>
        /// <param name="pluginContext">The plugin context.</param>
        public IEnumerable<ISocketContext> RemoveSocket(IPluginContext pluginContext)
        {
            var removed = GetSocketItems(pluginContext);

            _dict.Remove(pluginContext);

            foreach (var item in removed)
            {
                item.Dispose();
            }

            return removed.Select(x => x.SocketContext);
        }

        /// <summary>
        /// Removes all socket items from the dictionary for the given application context.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        public IEnumerable<ISocketContext> RemoveSocket(IApplicationContext applicationContext)
        {
            var removed = GetSocketItems(applicationContext);

            foreach (var applicationDict in _dict.Values)
            {
                applicationDict.Remove(applicationContext);
            }

            foreach (var item in removed)
            {
                item.Dispose();
            }

            return removed.Select(x => x.SocketContext);
        }

        /// <summary>
        /// Returns the socket items associated with the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">
        /// The plugin context to retrieve docket items for.
        /// </param>
        /// <returns>
        /// An IEnumerable of <see cref="SocketItem"/> associated with the specified 
        /// application context.
        /// </returns>
        public IEnumerable<SocketItem> GetSocketItems(IPluginContext pluginContext)
        {
            return _dict.Where(x => x.Key.Equals(pluginContext))
                .Select(x => x.Value)
                .SelectMany(x => x.Values)
                .SelectMany(x => x.Values);
        }

        /// <summary>
        /// Returns the items associated with the specified application context.
        /// </summary>
        /// <param name="applicationContext">
        /// The application context to retrieve items for.
        /// </param>
        /// <returns>
        /// An IEnumerable of <see cref="SocketItem"/> associated with the specified 
        /// application context.
        /// </returns>
        public IEnumerable<SocketItem> GetSocketItems(IApplicationContext applicationContext)
        {
            return _dict.Values
                .SelectMany(x => x)
                .Where(x => x.Key.Equals(applicationContext))
                .SelectMany(x => x.Value)
                .Select(x => x.Value);
        }

        /// <summary>
        /// Returns the socket item associated with the specified socket context.
        /// </summary>
        /// <param name="socketContext">
        /// The context of the socket to retrieve.
        /// </param>
        /// <returns>
        /// The <see cref="SocketItem"/> associated with the specified socket context, or 
        /// null if no such item exists.
        /// </returns>
        public SocketItem GetSocketItem(ISocketContext socketContext)
        {
            return _dict.Values
                .SelectMany(x => x.Values)
                .SelectMany(x => x.Values)
                .FirstOrDefault(x => x.SocketContext.Equals(socketContext));
        }

        /// <summary>
        /// Returns the socket items from the dictionary for a specific application 
        /// context and socket type.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="socketType">The type of the socket.</param>
        /// <returns>An IEnumerable of socket items.</returns>
        public IEnumerable<SocketItem> GetSocketItems(IApplicationContext applicationContext, Type socketType)
        {
            if (!typeof(ISocket).IsAssignableFrom(socketType))
            {
                return Enumerable.Empty<SocketItem>();
            }

            if (_dict.ContainsKey(applicationContext?.PluginContext))
            {
                var appContextDict = _dict[applicationContext?.PluginContext];

                if (appContextDict.TryGetValue(applicationContext, out Dictionary<Type, SocketItem> socketDict))
                {
                    if (socketDict.TryGetValue(socketType, out SocketItem value))
                    {
                        return [value];
                    }
                }
            }

            return Enumerable.Empty<SocketItem>();
        }

        /// <summary>
        /// Returns an enumeration of all containing socket contexts of a plugin.
        /// </summary>
        /// <param name="pluginContext">
        /// A context of a plugin whose sockets are to be registered.
        /// </param>
        /// <returns>An enumeration of socket contexts.</returns>
        public IEnumerable<ISocketContext> GetSockets(IPluginContext pluginContext)
        {
            if (_dict.TryGetValue(pluginContext, out var pluginResources))
            {
                return pluginResources
                    .SelectMany(x => x.Value)
                    .Select(x => x.Value.SocketContext);
            }

            return Enumerable.Empty<ISocketContext>();
        }

        /// <summary>
        /// Returns an enumeration of socket contexts.
        /// </summary>
        /// <param name="socketType">The socket type.</param>
        /// <returns>An enumeration of socket contexts.</returns>
        public IEnumerable<ISocketContext> GetSockets(Type socketType)
        {
            return _dict.Values
                .SelectMany(x => x.Values)
                .SelectMany(x => x.Values)
                .Where(x => x.SocketClass.Equals(socketType))
                .Select(x => x.SocketContext);
        }

        /// <summary>
        /// Returns an enumeration of socket contexts.
        /// </summary>
        /// <param name="socketType">The socket type.</param>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>An enumeration of socket contexts.</returns>
        public IEnumerable<ISocketContext> GetSockets(Type socketType, IApplicationContext applicationContext)
        {
            return _dict.Values
                .SelectMany(x => x.Values)
                .SelectMany(x => x.Values)
                .Where(x => x.SocketClass.Equals(socketType))
                .Where(x => x.SocketContext.ApplicationContext.Equals(applicationContext))
                .Select(x => x.SocketContext);
        }

        /// <summary>
        /// Returns an enumeration of socket contexts.
        /// </summary>
        /// <typeparam name="TSocket">The socket type.</typeparam>
        /// <param name="applicationContext">The context of the application.</param>
        /// <returns>An enumeration of socket contexts.</returns>
        public IEnumerable<ISocketContext> GetSockets<TSocket>(IApplicationContext applicationContext) where TSocket : ISocket
        {
            return _dict.Values
                .SelectMany(x => x.Values)
                .SelectMany(x => x.Values)
                .Where(x => x.SocketClass.Equals(typeof(TSocket)))
                .Where(x => x.SocketContext.ApplicationContext.Equals(applicationContext))
                .Select(x => x.SocketContext);
        }

        /// <summary>
        /// Returns the socket context.
        /// </summary>
        /// <param name="applicationContext">The context of the application.</param>
        /// <param name="socketId">The socket id.</param>
        /// <returns>An socket context or null.</returns>
        public ISocketContext GetSocket(IApplicationContext applicationContext, string socketId)
        {
            return _dict.Values
                .SelectMany(x => x.Values)
                .SelectMany(x => x.Values)
                .Where(x => x.SocketContext.ApplicationContext.Equals(applicationContext))
                .Where(x => x.SocketContext.EndpointId.Equals(socketId))
                .Select(x => x.SocketContext)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns the socket context.
        /// </summary>
        /// <param name="applicationId">The application id.</param>
        /// <param name="socketId">The socket id.</param>
        /// <returns>An socket context or null.</returns>
        public ISocketContext GetSocket(string applicationId, string socketId)
        {
            return _dict.Values
                .SelectMany(x => x.Values)
                .SelectMany(x => x.Values)
                .Where(x => x.SocketContext.ApplicationContext.ApplicationId.Equals(applicationId))
                .Where(x => x.SocketContext.EndpointId.Equals(socketId))
                .Select(x => x.SocketContext)
                .FirstOrDefault();
        }

        /// <summary>
        /// Checks if the dictionary contains the specified plugin context.
        /// </summary>
        /// <param name="pluginContext">The plugin context to check for.</param>
        /// <returns>
        /// True if the plugin context exists in the dictionary, otherwise false.
        /// </returns>
        public bool Contains(IPluginContext pluginContext)
        {
            return _dict.ContainsKey(pluginContext);
        }

        /// <summary>
        /// Checks if the dictionary contains the specified plugin context and application context.
        /// </summary>
        /// <param name="pluginContext">The plugin context to check for.</param>
        /// <param name="applicationContext">The application context to check for.</param>
        /// <returns>
        /// True if the plugin context and application context exist in the dictionary, 
        /// otherwise false.
        /// </returns>
        public bool Contains(IPluginContext pluginContext, IApplicationContext applicationContext)
        {
            return _dict.TryGetValue(pluginContext, out var appDict) && appDict.ContainsKey(applicationContext);
        }
    }
}