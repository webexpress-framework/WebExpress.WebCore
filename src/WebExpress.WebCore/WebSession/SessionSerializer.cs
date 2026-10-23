using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using WebExpress.WebCore.WebSession.Model;

namespace WebExpress.WebCore.WebSession
{
    /// <summary>
    /// Turns a session into bytes for the cluster store and back. Properties are stored with the
    /// name of their type, because a session holds properties of types the framework does not
    /// know - those of the plugins - and has to recreate exactly these types on another instance.
    /// </summary>
    /// <remarks>
    /// The type name in the store is never trusted on its own: a type is only recreated when it
    /// implements <see cref="ISessionProperty"/> and is concrete, so the stored name cannot steer
    /// the deserializer towards an arbitrary type. A property whose type is unknown on the reading
    /// instance - a plugin not deployed there - is dropped rather than failing the whole session.
    /// The properties form one json object keyed by type name, which is what lets two concurrent
    /// changes of one session be merged property by property.
    /// </remarks>
    internal static class SessionSerializer
    {
        private static readonly ConcurrentDictionary<string, Type> _types = new(StringComparer.Ordinal);

        private static readonly JsonSerializerOptions _options = new()
        {
            // read-only collections such as the parameter dictionary are filled, not replaced
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate
        };

        /// <summary>
        /// The serialized form of a session.
        /// </summary>
        /// <param name="Properties">The properties, keyed by type name.</param>
        /// <param name="Fingerprint">A hash of the properties only, so a pure access - which moves
        /// <see cref="Session.Updated"/> - is told apart from a real change.</param>
        internal sealed record Serialized(JsonObject Properties, byte[] Fingerprint);

        /// <summary>
        /// Serializes the properties of a session. A property the serializer cannot handle is
        /// left out and reported, so one unsuitable plugin type costs that property its way to
        /// the other instances - never the response of the request that used the session.
        /// </summary>
        /// <param name="session">The session.</param>
        /// <param name="unserializable">Receives the type and cause of every property left out.</param>
        /// <returns>The properties and their fingerprint.</returns>
        internal static Serialized Serialize(Session session, Action<Type, Exception> unserializable = null)
        {
            var properties = new JsonObject();

            KeyValuePair<Type, ISessionProperty>[] snapshot;
            lock (session.Properties)
            {
                snapshot = [.. session.Properties];
            }

            foreach (var (_, property) in snapshot.OrderBy(x => x.Key.FullName, StringComparer.Ordinal))
            {
                if (property is null)
                {
                    continue;
                }

                try
                {
                    properties[TypeName(property.GetType())] = JsonSerializer.SerializeToNode(property, property.GetType(), _options);
                }
                catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
                {
                    unserializable?.Invoke(property.GetType(), ex);
                }
            }

            return new Serialized(properties, Fingerprint(properties));
        }

        /// <summary>
        /// Computes the fingerprint of a set of properties.
        /// </summary>
        /// <param name="properties">The properties.</param>
        /// <returns>The hash of their json.</returns>
        internal static byte[] Fingerprint(JsonObject properties)
        {
            return SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(properties));
        }

        /// <summary>
        /// Builds the stored form of a session from its metadata and a set of properties.
        /// </summary>
        /// <param name="session">The session supplying id and times.</param>
        /// <param name="properties">The properties to store, possibly merged with another instance's.</param>
        /// <returns>The bytes to store.</returns>
        internal static byte[] Compose(Session session, JsonObject properties)
        {
            var document = new JsonObject
            {
                ["id"] = session.Id,
                ["created"] = session.Created,
                ["updated"] = session.Updated,
                ["properties"] = properties.DeepClone()
            };

            return JsonSerializer.SerializeToUtf8Bytes(document);
        }

        /// <summary>
        /// Reads only the properties of a stored session, as the other side of a merge.
        /// </summary>
        /// <param name="content">The stored bytes.</param>
        /// <returns>The properties, or null when the bytes are not a session.</returns>
        internal static JsonObject ReadProperties(byte[] content)
        {
            try
            {
                return JsonNode.Parse(content)?["properties"] as JsonObject;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Recreates a session.
        /// </summary>
        /// <param name="content">The stored bytes.</param>
        /// <param name="dropped">Receives the names of property types that could not be recreated.</param>
        /// <returns>The session and its stored properties, or null when the bytes are not a session.</returns>
        internal static (Session Session, JsonObject Properties)? Deserialize(byte[] content, Action<string> dropped = null)
        {
            JsonNode document;

            try
            {
                document = JsonNode.Parse(content);
            }
            catch (JsonException)
            {
                return null;
            }

            if (document is not JsonObject root
                || root["id"]?.GetValue<Guid>() is not Guid id)
            {
                return null;
            }

            var session = new Session(id)
            {
                Updated = root["updated"]?.GetValue<DateTime>() ?? DateTime.Now
            };
            session.Created = root["created"]?.GetValue<DateTime>() ?? session.Updated;

            var properties = root["properties"] as JsonObject ?? [];

            foreach (var (name, value) in properties)
            {
                var type = ResolveType(name);

                if (type is null)
                {
                    dropped?.Invoke(name);
                    continue;
                }

                try
                {
                    if (value.Deserialize(type, _options) is ISessionProperty property)
                    {
                        session.Properties[type] = property;
                    }
                }
                catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
                {
                    dropped?.Invoke(name);
                }
            }

            return (session, properties);
        }

        /// <summary>
        /// Returns the name a property type is stored under: the full name and the simple assembly
        /// name, without version, so a plugin update does not orphan the sessions of its users.
        /// </summary>
        /// <param name="type">The property type.</param>
        /// <returns>The stored name.</returns>
        private static string TypeName(Type type)
        {
            return type.FullName + ", " + type.Assembly.GetName().Name;
        }

        /// <summary>
        /// Finds a property type among the loaded assemblies, plugin load contexts included.
        /// </summary>
        /// <param name="name">The stored name.</param>
        /// <returns>The type, or null when it is unknown or not a session property.</returns>
        private static Type ResolveType(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (_types.TryGetValue(name, out var cached))
            {
                return cached;
            }

            var separator = name.LastIndexOf(", ", StringComparison.Ordinal);

            if (separator <= 0)
            {
                return null;
            }

            var typeName = name[..separator];
            var assemblyName = name[(separator + 2)..];

            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x => !x.IsDynamic && string.Equals(x.GetName().Name, assemblyName, StringComparison.Ordinal))
                .Select(x => x.GetType(typeName, false))
                .FirstOrDefault(x => x is not null);

            if (type is null || type.IsAbstract || type.IsInterface || !typeof(ISessionProperty).IsAssignableFrom(type))
            {
                return null;
            }

            _types[name] = type;

            return type;
        }
    }
}
