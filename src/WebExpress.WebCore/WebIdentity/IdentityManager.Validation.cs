using Microsoft.IdentityModel.JsonWebTokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPlugin;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// Asks the validators of an application, on every request, whether a signed identity may
    /// still act, so a disabled account or a withdrawn grant stops working before the token expires.
    /// </summary>
    public partial class IdentityManager
    {
        private readonly object _validatorGate = new();
        private readonly List<(IPluginContext Plugin, IApplicationContext Application, IIdentityValidator Validator)> _validators = [];

        /// <summary>
        /// Adds a validator that is asked about every identity of the application.
        /// </summary>
        /// <param name="validator">The validator.</param>
        /// <param name="applicationContext">The application whose requests it confirms.</param>
        public void RegisterValidator(IIdentityValidator validator, IApplicationContext applicationContext)
        {
            ArgumentNullException.ThrowIfNull(validator);
            ArgumentNullException.ThrowIfNull(applicationContext);

            lock (_validatorGate)
            {
                if (!_validators.Any(x => x.Application == applicationContext && ReferenceEquals(x.Validator, validator)))
                {
                    _validators.Add((null, applicationContext, validator));
                }
            }
        }

        /// <summary>
        /// Removes a validator from the application.
        /// </summary>
        /// <param name="validator">The validator.</param>
        /// <param name="applicationContext">The application it was added to.</param>
        /// <returns>True when the validator was registered.</returns>
        public bool UnregisterValidator(IIdentityValidator validator, IApplicationContext applicationContext)
        {
            lock (_validatorGate)
            {
                return _validators.RemoveAll(x => x.Application == applicationContext && ReferenceEquals(x.Validator, validator)) > 0;
            }
        }

        /// <summary>
        /// Constructs the validators a plugin defines, once for each of its applications.
        /// </summary>
        /// <param name="pluginContext">The plugin.</param>
        /// <param name="applicationContexts">The applications the plugin serves.</param>
        private void RegisterValidators(IPluginContext pluginContext, IEnumerable<IApplicationContext> applicationContexts)
        {
            var types = pluginContext?.Assembly?.GetTypes()
                .Where(x => x.IsClass && x.IsSealed && x.IsPublic && !x.ContainsGenericParameters && typeof(IIdentityValidator).IsAssignableFrom(x))
                .ToArray() ?? [];

            if (types.Length == 0)
            {
                return;
            }

            foreach (var applicationContext in applicationContexts ?? [])
            {
                foreach (var type in types)
                {
                    lock (_validatorGate)
                    {
                        if (_validators.Any(x => x.Plugin == pluginContext && x.Application == applicationContext && ValidatorType(x.Validator) == type))
                        {
                            continue;
                        }
                    }

                    var validator = CreateValidator(type, pluginContext, applicationContext);

                    lock (_validatorGate)
                    {
                        _validators.Add((pluginContext, applicationContext, validator));
                    }
                }
            }
        }

        /// <summary>
        /// Constructs a validator for an application. A validator that cannot be constructed is
        /// replaced by one that refuses every identity: the plugin meant to guard the application,
        /// and an application without its guard must not accept sign-ins as if none were intended.
        /// </summary>
        /// <param name="type">The validator type.</param>
        /// <param name="pluginContext">The plugin that defines it.</param>
        /// <param name="applicationContext">The application it confirms identities for.</param>
        /// <returns>The validator, or a refusing stand-in for the given type.</returns>
        private IIdentityValidator CreateValidator(Type type, IPluginContext pluginContext, IApplicationContext applicationContext)
        {
            var dependencies = new object[] { _componentHub, _httpServerContext, applicationContext, pluginContext };
            var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .OrderByDescending(x => x.GetParameters().Length)
                .FirstOrDefault(x => x.GetParameters().All(p => dependencies.Any(p.ParameterType.IsInstanceOfType)));

            if (constructor is null)
            {
                _httpServerContext?.Log?.Error($"The identity validator '{type.FullName}' has no constructor the discovery can satisfy; application '{applicationContext?.ApplicationId}' refuses every identity.");

                return new RefusingValidator(type);
            }

            try
            {
                return (IIdentityValidator)constructor.Invoke(constructor.GetParameters()
                    .Select(p => dependencies.First(p.ParameterType.IsInstanceOfType)).ToArray());
            }
            catch (Exception ex)
            {
                _httpServerContext?.Log?.Error($"The identity validator '{type.FullName}' could not be constructed; application '{applicationContext?.ApplicationId}' refuses every identity.");
                _httpServerContext?.Log?.Exception(ex is TargetInvocationException { InnerException: { } inner } ? inner : ex);

                return new RefusingValidator(type);
            }
        }

        /// <summary>
        /// Drops the validators of a removed plugin or application, so none outlives the code it belongs to.
        /// </summary>
        /// <param name="pluginContext">The removed plugin, or null.</param>
        /// <param name="applicationContext">The removed application, or null.</param>
        private void RemoveValidators(IPluginContext pluginContext, IApplicationContext applicationContext)
        {
            lock (_validatorGate)
            {
                _validators.RemoveAll(x => (pluginContext is not null && x.Plugin == pluginContext) ||
                    (applicationContext is not null && x.Application == applicationContext));
            }
        }

        /// <summary>
        /// Asks every validator of the request's application about a verified identity.
        /// </summary>
        /// <param name="identity">The identity the token carries, or null.</param>
        /// <param name="token">The verified token the identity was read from.</param>
        /// <param name="request">The request.</param>
        /// <returns>The identity when every validator accepts it; otherwise, null.</returns>
        private IIdentity Confirm(IIdentity identity, string token, IRequest request)
        {
            if (identity is null)
            {
                return null;
            }

            IIdentityValidator[] validators;
            lock (_validatorGate)
            {
                validators = [.. _validators.Where(x => x.Application == request.ApplicationContext).Select(x => x.Validator)];
            }

            if (validators.Length == 0)
            {
                return identity;
            }

            // the token was verified before, reading it again only takes the claims apart
            var jwt = new JsonWebToken(token);
            var context = new IdentityValidationContext
            {
                Identity = identity,
                Request = request,
                ApplicationContext = request.ApplicationContext,
                Grant = jwt.TryGetPayloadValue<string>("grant", out var grant) ? grant : null,
                TokenUse = jwt.TryGetPayloadValue<string>("token_use", out var use) ? use : null,
                IssuedAt = new DateTimeOffset(jwt.IssuedAt, TimeSpan.Zero)
            };

            foreach (var validator in validators)
            {
                try
                {
                    if (!validator.Validate(context))
                    {
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    // a validator that cannot answer refuses: letting the identity through would
                    // turn its outage into access for a disabled account
                    _httpServerContext?.Log?.Exception(ex);

                    return null;
                }
            }

            return identity;
        }

        /// <summary>
        /// Returns the validator type a registration stands for, so a refusing stand-in counts as
        /// the type it replaces and a repeated discovery does not add a second one.
        /// </summary>
        /// <param name="validator">The registered validator.</param>
        /// <returns>The type of the validator, or the type a stand-in replaces.</returns>
        private static Type ValidatorType(IIdentityValidator validator)
        {
            return validator is RefusingValidator refusing ? refusing.Type : validator.GetType();
        }

        /// <summary>
        /// Stands in for a validator that could not be constructed and refuses every identity.
        /// </summary>
        /// <param name="type">The validator type it replaces.</param>
        private sealed class RefusingValidator(Type type) : IIdentityValidator
        {
            /// <summary>
            /// Gets the validator type it replaces.
            /// </summary>
            public Type Type { get; } = type;

            /// <summary>
            /// Refuses the identity.
            /// </summary>
            /// <param name="context">The identity to confirm.</param>
            /// <returns>Always false.</returns>
            public bool Validate(IdentityValidationContext context)
            {
                return false;
            }
        }
    }
}
