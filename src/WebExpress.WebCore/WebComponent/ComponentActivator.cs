using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebStatusPage;

namespace WebExpress.WebCore.WebComponent
{
    /// <summary>
    /// Provides methods to create instances of components with dependency injection.
    /// </summary>
    public static class ComponentActivator
    {
        // a hub creates a component for every asset, page and fragment of every plugin - thousands
        // of the same few types - so the reflection behind an injection is looked up once per type.
        // the tables hold their types weakly: a type of a plugin keeps its load context alive, and
        // an uninstalled plugin could never be unloaded
        private static readonly ConditionalWeakTable<Type, ConcurrentDictionary<BindingFlags, (ConstructorInfo Constructor, ParameterInfo[] Parameters)[]>> _constructors = [];
        private static readonly ConditionalWeakTable<Type, PropertyInfo[]> _hubProperties = [];
        private static readonly ConditionalWeakTable<Type, StrongBox<Type>> _primaryInterfaces = [];
        private static readonly ConditionalWeakTable<Type, StrongBox<PropertyInfo>> _contextIdProperties = [];

        /// <summary>
        /// Returns the constructors of a type with their parameters, the one with the most
        /// parameters first, so the richest injection is tried before the simpler ones.
        /// </summary>
        /// <param name="type">The type, or null.</param>
        /// <param name="flags">The binding flags selecting the constructors.</param>
        /// <returns>The constructors, or null for no type.</returns>
        private static (ConstructorInfo Constructor, ParameterInfo[] Parameters)[] GetConstructors(Type type, BindingFlags flags)
        {
            if (type is null)
            {
                return null;
            }

            return _constructors.GetValue(type, _ => new()).GetOrAdd(flags, x => type
                .GetConstructors(x)
                .Select(constructor => (constructor, constructor.GetParameters()))
                .OrderByDescending(constructor => constructor.Item2.Length)
                .ToArray());
        }

        /// <summary>
        /// Returns the public instance properties of a component hub, through which its managers
        /// are injected.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <returns>The properties.</returns>
        private static PropertyInfo[] GetHubProperties(IComponentHub componentHub)
        {
            return _hubProperties.GetValue(componentHub.GetType(), type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance));
        }

        /// <summary>
        /// Returns the first interface of a manager type, under which the manager is injected.
        /// </summary>
        /// <param name="type">The manager type.</param>
        /// <returns>The interface, or null when the type implements none.</returns>
        private static Type GetPrimaryInterface(Type type)
        {
            return _primaryInterfaces.GetValue(type, x => new(x.GetInterfaces().FirstOrDefault())).Value;
        }

        /// <summary>
        /// Returns the property of a context type that holds its component id.
        /// </summary>
        /// <param name="type">The context type.</param>
        /// <returns>The property, or null when the context carries no id.</returns>
        private static PropertyInfo GetContextIdProperty(Type type)
        {
            return _contextIdProperties.GetValue(type, x => new(x.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => p.PropertyType == typeof(IComponentId)))).Value;
        }

        /// <summary>
        /// Returns the first manager injected under the given parameter type.
        /// </summary>
        /// <param name="managers">The managers of the hub.</param>
        /// <param name="parameterType">The type of the constructor parameter.</param>
        /// <returns>The manager, or null when none is injected under that type.</returns>
        private static IComponentManager FindManager(IComponentManager[] managers, Type parameterType)
        {
            foreach (var manager in managers)
            {
                if (GetPrimaryInterface(manager.GetType()) == parameterType)
                {
                    return manager;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the first additional argument that fits the given parameter type: one of
        /// exactly that type, or for a context or request parameter one implementing it.
        /// </summary>
        /// <param name="advancedParameters">The additional arguments of the call.</param>
        /// <param name="parameterType">The type of the constructor parameter.</param>
        /// <returns>The argument, or null when none fits.</returns>
        private static object FindAdvancedParameter(object[] advancedParameters, Type parameterType)
        {
            var byInterface = parameterType == typeof(IApplicationContext)
                || parameterType == typeof(IPageContext)
                || parameterType == typeof(IRequest);

            foreach (var parameter in advancedParameters)
            {
                var type = parameter.GetType();

                if (type == parameterType || (byInterface && type.GetInterfaces().Contains(parameterType)))
                {
                    return parameter;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates an instance of the specified response type with the component hub and 
        /// advanced parameters.
        /// </summary>
        /// <typeparam name="TResponse">
        /// The type of the response.
        /// </typeparam>
        /// <param name="responseType">
        /// The type of the response to create.
        /// </param>
        /// <param name="httpServerContext">
        /// The reference to the context of the host.
        /// </param>
        /// <param name="componentHub">
        /// The component hub to use for dependency injection.
        /// </param>
        /// <param name="statusMessage">
        /// Additional parameter with a status message to pass to the response's constructor.
        /// </param>
        /// <param name="advancedParameters">
        /// Additional parameters to pass to the component's constructor.
        /// </param>
        /// <returns>
        /// An instance of the specified response type.
        /// </returns>
        public static TResponse CreateInstance<TResponse>(Type responseType, IHttpServerContext httpServerContext, IComponentHub componentHub, StatusMessage statusMessage, params object[] advancedParameters)
            where TResponse : Response
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var constructors = GetConstructors(responseType, flags);

            if (constructors is not null)
            {
                foreach (var (constructor, parameters) in constructors)
                {
                    // injection
                    var properties = GetHubProperties(componentHub);

                    var parameterValues = parameters.Select(parameter =>
                        parameter.ParameterType == typeof(StatusMessage) ? statusMessage :
                        parameter.ParameterType == typeof(IComponentHub) ? componentHub :
                        parameter.ParameterType == typeof(IHttpServerContext) ? httpServerContext :
                        properties.Where(x => x.PropertyType == parameter.ParameterType)
                                  .FirstOrDefault()?
                                  .GetValue(componentHub) ??
                        advancedParameters.Where(x => x.GetType() == parameter.ParameterType)
                                  .FirstOrDefault() ?? null
                    ).ToArray();

                    if (constructor.Invoke(parameterValues) is TResponse component)
                    {
                        return component;
                    }
                }
            }

            return Activator.CreateInstance(responseType) as TResponse;
        }

        /// <summary>
        /// Creates an instance of the specified component type with the provided context, 
        /// component hub advanced parameters.
        /// </summary>
        /// <typeparam name="TComponentHub">
        /// The type of the component manager, which must implement <see cref="IComponentManager"/>.
        /// </typeparam>
        /// <param name="httpServerContext">
        /// The reference to the context of the host.
        /// </param>
        /// <param name="advancedParameters">
        /// Additional parameters to pass to the component's constructor.
        /// </param>
        /// <returns>
        /// An instance of the specified component type.
        /// </returns>
        public static TComponentHub CreateInstance<TComponentHub>(IHttpServerContext httpServerContext, params object[] advancedParameters)
            where TComponentHub : class, IComponentHub
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var constructors = GetConstructors(typeof(TComponentHub), flags);

            if (constructors is not null)
            {
                foreach (var (constructor, parameters) in constructors)
                {
                    // injection

                    var parameterValues = parameters.Select(parameter =>
                        parameter.ParameterType == typeof(IHttpServerContext) ? httpServerContext :

                        advancedParameters.Where(x => x.GetType() == parameter.ParameterType)
                                  .FirstOrDefault() ?? null
                    ).ToArray();

                    if (constructor.Invoke(parameterValues) is TComponentHub component)
                    {
                        return component;
                    }
                }
            }

            return Activator.CreateInstance(typeof(TComponentHub), advancedParameters) as TComponentHub;
        }

        /// <summary>
        /// Creates an instance of the specified component type with the provided context, 
        /// component hub advanced parameters.
        /// </summary>
        /// <typeparam name="TComponentManager">
        /// The type of the component manager, which must implement <see cref="IComponentManager"/>.
        /// </typeparam>
        /// <param name="componentType">
        /// The type of the component to create.
        /// </param>
        /// <param name="httpServerContext">
        /// The reference to the context of the host.
        /// </param>
        /// <param name="componentHub">
        /// The component hub to use for dependency injection.
        /// </param>
        /// <param name="advancedParameters">
        /// Additional parameters to pass to the component's constructor.
        /// </param>
        /// <returns>
        /// An instance of the specified component type.
        /// </returns>
        public static TComponentManager CreateInstance<TComponentManager>(Type componentType, IHttpServerContext httpServerContext, IComponentHub componentHub, params object[] advancedParameters)
            where TComponentManager : class, IComponentManager
        {
            try
            {
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var constructors = GetConstructors(componentType, flags);

                if (constructors is not null)
                {
                    foreach (var (constructor, parameters) in constructors)
                    {
                        // injection
                        var properties = GetHubProperties(componentHub);

                        var parameterValues = parameters.Select(parameter =>
                            parameter.ParameterType == typeof(IComponentHub)
                                ? componentHub
                                : parameter.ParameterType == typeof(IHttpServerContext)
                                    ? httpServerContext
                                    : properties.FirstOrDefault(x => x.PropertyType == parameter.ParameterType)?
                                        .GetValue(componentHub)
                                            ?? advancedParameters.FirstOrDefault(x => x.GetType() == parameter.ParameterType)
                                                ?? null
                        ).ToArray();

                        if (constructor?.Invoke(parameterValues) is TComponentManager component)
                        {
                            return component;
                        }
                    }
                }

                return Activator.CreateInstance(componentType) as TComponentManager;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"CreateInstance failed: {componentType.FullName} with {httpServerContext}");
                Console.WriteLine(ex.InnerException?.Message);
                Console.WriteLine(ex.InnerException);
                httpServerContext?.Log?.Exception(ex);
            }

            return null;
        }

        /// <summary>
        /// Creates an instance of the specified component type with the provided context, 
        /// component hub advanced parameters.
        /// </summary>
        /// <typeparam name="TComponent">
        /// The type of the component manager, which must implement <see cref="IComponentManager"/>.
        /// </typeparam>
        /// <param name="httpServerContext">
        /// The reference to the context of the host.
        /// </param>
        /// <param name="componentHub">
        /// The component hub to use for dependency injection.
        /// </param>
        /// <param name="componentType">
        /// The type of the component to create.
        /// </param>
        /// <param name="advancedParameters">
        /// Additional parameters to pass to the component's constructor.
        /// </param>
        /// <returns>
        /// An instance of the specified component type.
        /// </returns>
        public static TComponent CreateInstance<TComponent>(IHttpServerContext httpServerContext, IComponentHub componentHub, Type componentType, params object[] advancedParameters)
            where TComponent : IComponent
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var constructors = GetConstructors(componentType, flags);

            if (constructors is not null)
            {
                foreach (var (constructor, parameters) in constructors)
                {
                    // injection
                    var properties = GetHubProperties(componentHub);

                    var parameterValues = parameters.Select(parameter =>
                        parameter.ParameterType == typeof(IComponentHub) ? componentHub :
                        parameter.ParameterType == typeof(IHttpServerContext) ? httpServerContext :
                        properties.Where(x => x.PropertyType == parameter.ParameterType)
                                  .FirstOrDefault()?
                                  .GetValue(componentHub) ??
                        advancedParameters.Where(x => x.GetType() == parameter.ParameterType)
                                  .FirstOrDefault() ?? null
                    ).ToArray();

                    if (constructor.Invoke(parameterValues) is TComponent component)
                    {
                        return component;
                    }
                }
            }

            return (TComponent)Activator.CreateInstance(componentType);
        }

        /// <summary>
        /// Creates an instance of the specified component type with the provided context, 
        /// component hub advanced parameters.
        /// </summary>
        /// <typeparam name="TComponent">
        /// The type of the component, which must implement <see cref="IComponent"/>.
        /// </typeparam>
        /// <param name="httpServerContext">
        /// The reference to the context of the host.
        /// </param>
        /// <param name="componentHub">
        /// The component hub to use for dependency injection.
        /// </param>
        /// <param name="advancedParameters">
        /// Additional parameters to pass to the component's constructor.
        /// </param>
        /// <returns>
        /// An instance of the specified component type.
        /// </returns>
        public static TComponent CreateInstance<TComponent>(IHttpServerContext httpServerContext, IComponentHub componentHub, params object[] advancedParameters)
            where TComponent : class, IComponent
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var componentType = typeof(TComponent);
            var constructors = GetConstructors(componentType, flags);

            if (constructors is not null)
            {
                foreach (var (constructor, parameters) in constructors)
                {
                    // injection
                    var properties = GetHubProperties(componentHub);

                    var parameterValues = parameters.Select(parameter =>
                        parameter.ParameterType == typeof(IComponentHub) ? componentHub :
                        parameter.ParameterType == typeof(IHttpServerContext) ? httpServerContext :
                        properties.Where(x => x.PropertyType == parameter.ParameterType)
                            .FirstOrDefault()?
                            .GetValue(componentHub) ??
                        advancedParameters.Where(x => x is not null)
                            .Where(x => x.GetType() == parameter.ParameterType)
                            .FirstOrDefault() ?? null
                    ).ToArray();

                    if (constructor.Invoke(parameterValues) is TComponent component)
                    {
                        return component;
                    }
                }
            }

            return Activator.CreateInstance(componentType) as TComponent;
        }

        /// <summary>
        /// Creates an instance of the specified component type with the provided context and 
        /// component hub and advanced parameters.
        /// </summary>
        /// <typeparam name="TComponent">
        /// The type of the component, which must implement <see cref="IComponent"/>.
        /// </typeparam>
        /// <typeparam name="TContext">
        /// The type of the context, which must implement <see cref="IContext"/>.
        /// </typeparam>
        /// <param name="componentType">
        /// The type of the component to create.
        /// </param>
        /// <param name="context">
        /// The context to pass to the component's constructor.
        /// </param>
        /// <param name="httpServerContext">
        /// The reference to the context of the host.
        /// </param>
        /// <param name="componentHub">
        /// The component hub to use for dependency injection.
        /// </param>
        /// <param name="advancedParameters">
        /// Additional parameters to pass to the component's constructor.
        /// </param>
        /// <returns>
        /// An instance of the specified component type.
        /// </returns>
        public static TComponent CreateInstance<TComponent, TContext>(Type componentType, TContext context, IHttpServerContext httpServerContext, IComponentHub componentHub, params object[] advancedParameters)
            where TComponent : class, IComponent
            where TContext : IContext
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var constructors = GetConstructors(componentType, flags);

            if (constructors is not null)
            {
                foreach (var (constructor, parameters) in constructors)
                {
                    // injection - a plain loop, as this overload creates every asset, page and
                    // fragment of a hub and the per-parameter queries were its main allocation
                    IComponentManager[] managers = null;
                    var parameterValues = new object[parameters.Length];

                    for (var i = 0; i < parameters.Length; i++)
                    {
                        var parameterType = parameters[i].ParameterType;

                        if (parameterType == typeof(IComponentHub))
                        {
                            parameterValues[i] = componentHub;
                        }
                        else if (parameterType == typeof(IHttpServerContext))
                        {
                            parameterValues[i] = httpServerContext;
                        }
                        else if (parameterType == typeof(TContext))
                        {
                            parameterValues[i] = context;
                        }
                        else if (parameterType == typeof(IComponentId))
                        {
                            parameterValues[i] = GetContextIdProperty(context.GetType())?.GetValue(context);
                        }
                        else
                        {
                            managers ??= [.. componentHub.Managers];
                            parameterValues[i] = FindManager(managers, parameterType)
                                ?? FindAdvancedParameter(advancedParameters, parameterType);
                        }
                    }

                    if (constructor.Invoke(parameterValues) is TComponent component)
                    {
                        return component;
                    }
                }
            }

            return Activator.CreateInstance(componentType) as TComponent;
        }

        /// <summary>
        /// Creates an instance of the specified component type with the provided context and 
        /// component hub and advanced parameters.
        /// </summary>
        /// <typeparam name="TContext">
        /// The type of the context, which must implement <see cref="IContext"/>.
        /// </typeparam>
        /// <param name="componentType">
        /// The type of the component to create.
        /// </param>
        /// <param name="context">
        /// The context to pass to the component's constructor.
        /// </param>
        /// <param name="httpServerContext">
        /// The reference to the context of the host.
        /// </param>
        /// <param name="componentHub">
        /// The component hub to use for dependency injection.
        /// </param>
        /// <param name="advancedParameters">
        /// Additional parameters to pass to the component's constructor.
        /// </param>
        /// <returns>
        /// An instance of the specified component type.
        /// </returns>
        public static IComponent CreateInstance<TContext>(Type componentType, TContext context, IHttpServerContext httpServerContext, IComponentHub componentHub, params object[] advancedParameters)
            where TContext : IContext
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var constructors = GetConstructors(componentType, flags);

            if (constructors is not null)
            {
                foreach (var (constructor, parameters) in constructors)
                {
                    // injection
                    var properties = GetHubProperties(componentHub);
                    var contextIdProperty = GetContextIdProperty(context.GetType());

                    var parameterValues = parameters.Select(parameter =>
                        parameter.ParameterType == typeof(IComponentHub) ? componentHub :
                        parameter.ParameterType == typeof(IHttpServerContext) ? httpServerContext :
                        parameter.ParameterType == typeof(TContext) ? context :
                        parameter.ParameterType == typeof(IComponentId) ? contextIdProperty?.GetValue(context) :
                        properties.Where(x => x.PropertyType == parameter.ParameterType)
                                  .FirstOrDefault()?
                                  .GetValue(componentHub) ??
                        advancedParameters.Where(x => x.GetType() == parameter.ParameterType)
                                  .FirstOrDefault() ?? null
                    ).ToArray();

                    if (constructor.Invoke(parameterValues) is IComponent component)
                    {
                        return component;
                    }
                }
            }

            return Activator.CreateInstance(componentType) as IComponent;
        }
    }
}
