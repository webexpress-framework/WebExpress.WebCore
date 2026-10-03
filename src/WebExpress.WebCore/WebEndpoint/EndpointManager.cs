using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.WebEndpoint
{
    /// <summary>
    /// The endpoint manager manages WebExpress elements, which can be called with a URI (Uniform Resource Identifier).
    /// </summary>
    public sealed class EndpointManager : IEndpointManager, ISystemComponent
    {
        private static readonly string[] _namespacePrefixes = ["page", "pages", "webpage", "webpages", "website", "www", "web"];
        private static readonly string _indexPrefix = "index";
        private static readonly string[] _classSuffixes = ["page"];
        private readonly IHttpServerContext _httpServerContext;
        private readonly Dictionary<Type, EndpointRegistration> _registrations = [];

        // an assembly's type set is immutable at runtime, so it is memoized here. CreateEndpointRoute
        // would otherwise call Assembly.GetTypes() (a full type enumeration) for every route segment
        // of every endpoint during registration - repeatedly, because the segment query is enumerated
        // more than once.
        private static readonly ConcurrentDictionary<Assembly, Type[]> _assemblyTypes = new();

        /// <summary>
        /// An event that fires when an endpoint is added.
        /// </summary>
        public event EventHandler<IEndpointContext> AddEndpoint;

        /// <summary>
        /// An event that fires when an endpoint is removed.
        /// </summary>
        public event EventHandler<IEndpointContext> RemoveEndpoint;

        /// <summary>
        /// Gets all endpoints contexts.
        /// </summary>
        public IEnumerable<IEndpointContext> Endpoints => _registrations.Values.SelectMany(x => x.EndpointsResolver());

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private EndpointManager(IHttpServerContext httpServerContext)
        {
            _httpServerContext = httpServerContext;

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate("webexpress.webcore:endpointmanager.initialization")
            );
        }

        /// <summary>
        /// Registers an endpoint context type.
        /// </summary>
        /// <typeparam name="TEndpointContext">The type of the endpoint context.</typeparam>
        /// <param name="endpointRegistration">The registration details containing the callback functions.</param>
        public void Register<TEndpointContext>(EndpointRegistration endpointRegistration) where TEndpointContext : IEndpointContext
        {
            var type = typeof(TEndpointContext);
            if (!_registrations.ContainsKey(type))
            {
                _registrations[type] = endpointRegistration;

                endpointRegistration.AddEndpoint += OnAddEndpoint;
                endpointRegistration.RemoveEndpoint += OnRemoveEndpoint;
            }
        }

        /// <summary>
        /// Removes the registration for a specific endpoint context type.
        /// </summary>
        /// <typeparam name="TEndpointContext">The type of the endpoint context.</typeparam>
        public void Remove<TEndpointContext>() where TEndpointContext : IEndpointContext
        {
            var type = typeof(TEndpointContext);
            _registrations.Remove(type, out var endpointRegistration);

            endpointRegistration.AddEndpoint -= OnAddEndpoint;
            endpointRegistration.RemoveEndpoint -= OnRemoveEndpoint;
        }

        /// <summary>
        /// Returns an enumeration of endpoint contexts.
        /// </summary>
        /// <param name="endpointType">The endpoint type.</param>
        /// <param name="applicationContext">The application context.</param>
        /// <returns>An enumeration of endpoint contexts.</returns>
        public IEnumerable<IEndpointContext> GetEndpoints(Type endpointType, IApplicationContext applicationContext = null)
        {
            if (endpointType is null)
            {
                return [];
            }

            return _registrations.SelectMany(x => x.Value.EndpointResolver(endpointType, applicationContext));
        }

        /// <summary>
        /// Handles a request and returns a response.
        /// </summary>
        /// <param name="request">The request to handle.</param>
        /// <param name="endpointContext">The context of the endpoint handling the request.</param>
        /// <returns>The response generated by the endpoint.</returns>
        public IResponse HandleRequest(IRequest request, IEndpointContext endpointContext)
        {
            if (endpointContext is null || !_registrations.TryGetValue(endpointContext.GetType(), out var registration))
            {
                return null;
            }

            return registration.HandleRequest(request, endpointContext);
        }

        /// <summary>
        /// Raises the event when an endpoint is added.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="endpointContext">The context of the endpoint being added.</param>
        private void OnAddEndpoint(object sender, IEndpointContext endpointContext)
        {
            AddEndpoint?.Invoke(sender, endpointContext);
        }

        /// <summary>
        /// Raises the event when an endpoint is removed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="endpointContext">The context of the endpoint being removed.</param>
        private void OnRemoveEndpoint(object sender, IEndpointContext endpointContext)
        {
            RemoveEndpoint?.Invoke(sender, endpointContext);
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Returns the types declared in the given assembly, memoized for the lifetime of the
        /// process. The type set of a loaded assembly does not change, so caching it avoids the
        /// repeated full enumeration that route creation would otherwise trigger per segment.
        /// </summary>
        /// <param name="assembly">The assembly whose types are requested.</param>
        /// <returns>The assembly's declared types.</returns>
        private static Type[] GetAssemblyTypes(Assembly assembly)
        {
            return _assemblyTypes.GetOrAdd(assembly, a => a.GetTypes());
        }

        /// <summary>
        /// Returns the route of an endpoint based on the class type, application context and segment attributes.
        /// </summary>
        /// <param name="classType">The type of the class.</param>
        /// <param name="contextRoute">The application context route.</param>
        /// <param name="segment">The segment attribute.</param>
        /// <param name="intermediateSegments">The intermediate segments.</param>
        /// <param name="namespacePrefixes">The namespace prefixes.</param>
        /// <returns>The route of the endpoint.</returns>
        public static IRoute CreateEndpointRoute
        (
            Type classType,
            IRoute contextRoute,
            ISegmentAttribute segment,
            IEnumerable<IUriPathSegment> intermediateSegments = null,
            string[] namespacePrefixes = null
        )
        {
            var assemblyName = classType.Assembly.GetName().Name;
            var fullClassName = classType.FullName;
            var className = _classSuffixes?.FirstOrDefault(s => classType.Name.ToLowerInvariant().EndsWith(s, StringComparison.OrdinalIgnoreCase)) is string suffix
                ? classType.Name.ToLowerInvariant()[..^suffix.Length]
                : classType.Name.ToLowerInvariant();
            var segments = (fullClassName.Length - classType.Name.Length - 1 > assemblyName.Length)
                 ? fullClassName[(assemblyName.Length + 1)..^(classType.Name.Length + 1)].ToLowerInvariant().Split('.', StringSplitOptions.RemoveEmptyEntries)
                 : [];

            var segmentMapping = segments.Select((segment, index) => new
            {
                FullNamespace = $"{assemblyName}.{string.Join(".", segments.Take(index + 1))}",
                Segment = segment
            });

            var segmentAttributesMapping = segmentMapping.Select(s =>
            {
                var segmentResult = default(IUriPathSegment);
                var name = default(string);
                var description = default(string);
                var icon = default(IIcon);
                var hidden = false;

                var segmentInfoType = GetAssemblyTypes(classType.Assembly)
                    .Where(t => t.IsClass)
                    .Where(t => t.Namespace?.ToLowerInvariant() == s.FullNamespace.ToLowerInvariant())
                    .FirstOrDefault(t => t.Name.StartsWith("Index", StringComparison.OrdinalIgnoreCase));

                var nameAttr = segmentInfoType?.CustomAttributes
                        .FirstOrDefault(x => x.AttributeType == typeof(TitleAttribute));
                var descAttr = segmentInfoType?.CustomAttributes
                    .FirstOrDefault(x => x.AttributeType == typeof(DescriptionAttribute));
                var iconAttr = segmentInfoType?.CustomAttributes
                    .FirstOrDefault(x => x.AttributeType.IsGenericType &&
                                         x.AttributeType.GetGenericTypeDefinition() == typeof(WebIconAttribute<>));
                var hiddenAttr = segmentInfoType?.CustomAttributes
                     .FirstOrDefault(x => x.AttributeType == typeof(SegmentHiddenAttribute));

                if (segmentInfoType is not null)
                {
                    var segAttrType = segmentInfoType.CustomAttributes
                        .Where(x => x.AttributeType.GetInterfaces().Contains(typeof(ISegmentAttribute)))
                        .Select(x => x.AttributeType)
                        .FirstOrDefault();

                    var segInstance = segAttrType is not null
                        ? segmentInfoType.GetCustomAttribute(segAttrType, false) as ISegmentAttribute
                        : null;

                    segmentResult = segInstance?.ToPathSegment();
                    segmentResult?.IsHidden = hiddenAttr is not null;
                    name = nameAttr?.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    description = descAttr?.ConstructorArguments.FirstOrDefault().Value?.ToString();
                    icon = iconAttr is not null
                        ? Activator.CreateInstance(iconAttr.AttributeType.GenericTypeArguments.FirstOrDefault()) as IIcon
                        : null;
                    hidden = hiddenAttr is not null;
                }

                return new
                {
                    Segment = segmentResult ?? new UriPathSegmentConstant(s.Segment)
                    {
                        IsHidden = segmentInfoType is null || hiddenAttr is not null
                    },
                    Name = name,
                    Description = description,
                    Icon = icon,
                    Hidden = segmentResult is null || hidden
                };
            }).ToList();

            // materialized once so the reflection-heavy projection runs a single time per segment;
            // leading namespace-prefix segments are then dropped via a computed skip count instead of
            // re-enumerating (and thus re-projecting) the sequence multiple times.
            var skip = 0;
            var firstMapping = segmentAttributesMapping.FirstOrDefault();
            if (firstMapping is not null && _namespacePrefixes.Contains(firstMapping.Segment?.ToString()))
            {
                skip = 1;
            }

            firstMapping = segmentAttributesMapping.Skip(skip).FirstOrDefault();
            if (firstMapping is not null && (namespacePrefixes ?? []).Contains(firstMapping.Segment?.ToString()))
            {
                skip++;
            }

            var endpointRoute = (intermediateSegments ?? [])
                .Concat(segmentAttributesMapping.Skip(skip).Where(x => !x.Segment.IsEmpty).Select(x => x.Segment));

            var classSegment = !className.StartsWith(_indexPrefix)
                           ? segment?.ToPathSegment() ?? new UriPathSegmentConstant(className)
                           : null;
            var uri = RouteEndpoint.Combine(contextRoute, endpointRoute)
                .Concat(classSegment);

            return uri;
        }

        /// <summary>
        /// Creates instances of attributes from a collection of <see cref="CustomAttributeData"/> objects.
        /// </summary>
        /// <param name="customAttributesData">A collection of objects representing the metadata of attributes.</param>
        /// <returns>An enumerable of instances created from the provided metadata. If
        /// no attributes are instantiated, an empty collection is returned.</returns>
        public static IEnumerable<Attribute> GetAttributeInstances(IEnumerable<CustomAttributeData> customAttributesData)
        {
            List<Attribute> attributeInstances = [];

            foreach (var attrData in customAttributesData)
            {
                var attributeType = attrData.AttributeType;
                var constructorArgs = new List<object>();

                // extract constructor arguments
                foreach (var arg in attrData.ConstructorArguments)
                {
                    constructorArgs.Add(arg.Value);
                }

                // instantiate the attribute using reflection
                if (Activator.CreateInstance(attributeType, [.. constructorArgs]) is Attribute attributeInstance)
                {
                    attributeInstances.Add(attributeInstance);
                }
            }

            return attributeInstances;
        }
    }
}
