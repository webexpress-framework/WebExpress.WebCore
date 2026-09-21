using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace WebExpress.WebCore.WebSession.Model
{
    /// <summary>
    /// Represents a session.Through a session, session data can be assigned to
    /// a user. Session data is stored on the server side, turning the stateless 
    /// http protocol into a state-based one.
    /// </summary>
    public class Session
    {
        /// <summary>
        /// Gets the session id.
        /// </summary>
        /// <remarks>
        /// The id identifies optional application state and never authenticates a user.
        /// Applications may regenerate it when replacing sensitive application state.
        /// </remarks>
        public Guid Id { get; internal set; }

        /// <summary>
        /// Gets the creation time.
        /// </summary>
        public DateTime Created { get; private set; }

        /// <summary>
        /// Gets or sets the time of the last access.
        /// </summary>
        public DateTime Updated { get; set; }

        /// <summary>
        /// Gets properties for the session.
        /// </summary>
        public Dictionary<Type, ISessionProperty> Properties { get; private set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Session()
            : this(Guid.NewGuid())
        {
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="id">The session id.</param>
        public Session(Guid id)
        {
            Id = id;
            Created = DateTime.Now;
            Updated = DateTime.Now;

            Properties = [];
        }

        /// <summary>
        /// Returns a session property.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <returns>The property or null.</returns>
        public T GetProperty<T>() where T : class, ISessionProperty
        {
            lock (Properties)
            {
                if (Properties.ContainsKey(typeof(T)))
                {
                    return Properties[typeof(T)] as T;
                }
            }

            return default;
        }

        /// <summary>
        /// Returns a property if it already exists. Otherwise, a new property will be created.
        /// </summary>
        /// <typeparam name="TSessionProperty">The type of the property.</typeparam>
        /// <param name="parameters">
        /// The parameters to pass to the constructor of the property if it needs to be created.
        /// </param>
        /// <returns>The property or null if it cannot be created.</returns>
        public TSessionProperty GetOrCreateProperty<TSessionProperty>(params object[] parameters)
            where TSessionProperty : class, ISessionProperty
        {
            var type = typeof(TSessionProperty);
            lock (Properties)
            {
                if (Properties.ContainsKey(typeof(TSessionProperty)))
                {
                    return Properties[type] as TSessionProperty;
                }

                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                var constructors = type.GetConstructors(flags);

                if (constructors is not null || parameters.Length > 0)
                {
                    foreach (var constructor in constructors.OrderByDescending(x => x.GetParameters().Length))
                    {
                        // injection
                        var constructorParameters = constructor.GetParameters();
                        var parameterValues = constructorParameters.Select
                        (
                            x => parameters.FirstOrDefault
                            (
                                y => y is not null &&
                                (
                                    y.GetType() == x.ParameterType ||
                                    x.ParameterType.IsAssignableFrom(y.GetType()) ||
                                    y.GetType().IsSubclassOf(x.ParameterType)
                                )
                            ) ?? null
                        ).ToArray();

                        if (constructor.Invoke(parameterValues) is TSessionProperty injectionProperty)
                        {
                            SetProperty(injectionProperty);

                            return injectionProperty;
                        }
                    }
                }

                var property = Activator.CreateInstance<TSessionProperty>();
                SetProperty(property);

                return property;
            }
        }

        /// <summary>
        /// Sets a property.
        /// </summary>
        /// <param name="property">The property to set.</param>
        public void SetProperty(ISessionProperty property)
        {
            lock (Properties)
            {
                if (!Properties.ContainsKey(property.GetType()))
                {
                    Properties.Add(property.GetType(), property);
                }

                Properties[property.GetType()] = property;
            }
        }

        /// <summary>
        /// Removes a property.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        public void RemoveProperty<T>() where T : class, ISessionProperty
        {
            lock (Properties)
            {
                Properties.Remove(typeof(T));
            }
        }

    }
}
