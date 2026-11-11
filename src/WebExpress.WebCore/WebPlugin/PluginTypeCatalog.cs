using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.WebPlugin
{
    /// <summary>
    /// Collects the types a plugin contributes to an extension point, so a manager - of the
    /// framework or of an application - does not have to repeat the scan of the plugin
    /// assemblies, the plugin and application events and the bookkeeping of who provides what.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A type is an item when it is a class, public at top level (a nested type is not, because
    /// <see cref="Type.IsPublic"/> is false for it), sealed, not abstract, not an open generic and
    /// assignable to the contract - and, without a factory, has a public parameterless
    /// constructor. A type that misses any of these is skipped <b>silently</b>, the way the
    /// framework skips a page or a fragment that does not follow its convention; a plugin should
    /// therefore test that every item it ships is found.
    /// </para>
    /// <para>
    /// A catalog is meant to be held as a field of a manager, never inherited from: the manager
    /// attaches it in its constructor, disposes it in its own <c>Dispose</c> and exposes what its
    /// interface promises. That leaves a manager free to choose its base class and to hold several
    /// catalogs.
    /// </para>
    /// <para>
    /// Every plugin's offer is kept, in the order the plugins arrived, and the item in use under a
    /// key is the latest offer of it. A plugin that leaves takes only what it offered: a key it
    /// still provides falls back to the latest remaining offer of another plugin, and is removed
    /// only when nobody offers it any more; a key another plugin took over is left alone. An item
    /// given to <see cref="Register(TItem)"/> is an offer of nobody and no plugin removal takes
    /// it away.
    /// </para>
    /// <para>
    /// The catalog is thread-safe. Types are scanned and items constructed outside its lock, and
    /// <see cref="Added"/> and <see cref="Removed"/> are raised after it was released, so a
    /// constructor or a handler may call back into the catalog. Items the catalog constructed are
    /// disposed when they leave it, if they are disposable; registered items belong to their caller.
    /// </para>
    /// </remarks>
    /// <typeparam name="TItem">The type of the items.</typeparam>
    public sealed class PluginTypeCatalog<TItem> : IDisposable
        where TItem : class
    {
        private readonly Func<TItem, string> _keyOf;
        private readonly Type _contract;
        private readonly Func<PluginTypeOrigin, TItem> _factory;
        private readonly Lock _sync = new();
        private readonly List<Offer> _offers = [];
        private readonly Dictionary<Slot, Offer> _inUse = new(SlotComparer.Instance);
        private IComponentHub _componentHub;
        private IHttpServerContext _httpServerContext;
        private long _version;
        private bool _disposed;

        /// <summary>
        /// Occurs when an item becomes the one in use under its key - offered first, taking over
        /// from another offer, or falling back after the offer above it left.
        /// </summary>
        public event EventHandler<PluginTypeCatalogEventArgs<TItem>> Added;

        /// <summary>
        /// Occurs when an item stops being the one in use under its key - its plugin or
        /// application left, another offer took over, or the catalog was disposed.
        /// </summary>
        public event EventHandler<PluginTypeCatalogEventArgs<TItem>> Removed;

        /// <summary>
        /// Gets what the items belong to.
        /// </summary>
        public PluginTypeBinding Binding { get; }

        /// <summary>
        /// Gets a number that moves whenever an item comes, goes or is replaced, so a cached
        /// projection of the items - a picker, a menu - can tell it is stale without comparing.
        /// </summary>
        public long Version => Interlocked.Read(ref _version);

        /// <summary>
        /// Gets the items in use, in the order their offers arrived. In a catalog bound per
        /// application these are the items of every application.
        /// </summary>
        public IReadOnlyList<TItem> Items
        {
            get
            {
                lock (_sync)
                {
                    return Snapshot(x => true);
                }
            }
        }

        /// <summary>
        /// Creates a catalog of the public sealed implementations of <typeparamref name="TItem"/>,
        /// built through their parameterless constructor and shared by the whole host - the shape
        /// of most extension points an application defines.
        /// </summary>
        /// <param name="keyOf">Answers the key of an item. Keys are trimmed and compared without
        /// case; an item without one is logged and skipped.</param>
        public PluginTypeCatalog(Func<TItem, string> keyOf)
            : this(keyOf, typeof(TItem), null, PluginTypeBinding.Plugin)
        {
        }

        /// <summary>
        /// Creates a catalog whose items are built from the discovered types by a factory - for a
        /// type that needs its plugin or application to be constructed, or an item that wraps the
        /// discovered type rather than being it.
        /// </summary>
        /// <param name="keyOf">Answers the key of an item. Keys are trimmed and compared without
        /// case; an item without one is logged and skipped.</param>
        /// <param name="contract">The type a discovered type must be assignable to.</param>
        /// <param name="factory">Builds the item of a discovered type; <see langword="null"/> uses the
        /// public parameterless constructor, which then also becomes part of the convention, and
        /// requires the contract to be an item type. A factory answering <see langword="null"/>
        /// declines the type.</param>
        /// <param name="binding">What the items belong to.</param>
        /// <exception cref="ArgumentNullException">The key function or the contract is missing.</exception>
        /// <exception cref="ArgumentException">Without a factory, the contract is no item type.</exception>
        public PluginTypeCatalog(Func<TItem, string> keyOf, Type contract, Func<PluginTypeOrigin, TItem> factory, PluginTypeBinding binding = PluginTypeBinding.Plugin)
        {
            ArgumentNullException.ThrowIfNull(keyOf);
            ArgumentNullException.ThrowIfNull(contract);

            if (factory is null && !typeof(TItem).IsAssignableFrom(contract))
            {
                throw new ArgumentException($"Without a factory, the contract must be assignable to {typeof(TItem).FullName}.", nameof(contract));
            }

            _keyOf = keyOf;
            _contract = contract;
            _factory = factory;
            Binding = binding;
        }

        /// <summary>
        /// Follows the plugins of a host: collects the plugins already loaded, because a manager is
        /// built before the plugins arrive and each pass alone misses half of them, and subscribes
        /// to their arrival and removal - and, in a catalog bound per application, to those of the
        /// applications.
        /// </summary>
        /// <param name="componentHub">The hub whose plugins are followed.</param>
        /// <param name="httpServerContext">The host context whose log receives the skipped types. May be null.</param>
        /// <exception cref="InvalidOperationException">The catalog already follows a hub.</exception>
        public void Attach(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            ArgumentNullException.ThrowIfNull(componentHub);

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                if (_componentHub is not null)
                {
                    throw new InvalidOperationException("The catalog already follows a component hub.");
                }

                _componentHub = componentHub;
                _httpServerContext = httpServerContext;
            }

            var plugins = componentHub.PluginManager;

            if (plugins is null)
            {
                return;
            }

            // subscribe before the sweep, so a plugin arriving in between is not missed; collecting
            // a plugin twice does nothing
            plugins.AddPlugin += OnAddPlugin;
            plugins.RemovePlugin += OnRemovePlugin;

            if (Binding == PluginTypeBinding.Application && componentHub.ApplicationManager is { } applications)
            {
                applications.AddApplication += OnAddApplication;
                applications.RemoveApplication += OnRemoveApplication;
            }

            foreach (var plugin in plugins.Plugins?.ToArray() ?? [])
            {
                Collect(plugin);
            }
        }

        /// <summary>
        /// Collects the items of an assembly that is no plugin - the way a unit test, which has no
        /// plugin manager, gets the items a manager would find in a host. Such items belong to no
        /// plugin and stay until the catalog is disposed.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <exception cref="InvalidOperationException">The catalog is bound per application.</exception>
        public void Collect(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            Demand(PluginTypeBinding.Plugin);

            CollectOffer(null, null, assembly);
        }

        /// <summary>
        /// Collects the items of a plugin. A catalog bound per application collects them for the
        /// applications the attached hub associates with the plugin. Collecting a plugin again does
        /// nothing.
        /// </summary>
        /// <param name="pluginContext">The plugin.</param>
        public void Collect(IPluginContext pluginContext)
        {
            ArgumentNullException.ThrowIfNull(pluginContext);

            if (Binding == PluginTypeBinding.Plugin)
            {
                CollectOffer(pluginContext, null, pluginContext.Assembly);

                return;
            }

            IComponentHub componentHub;

            lock (_sync)
            {
                componentHub = _componentHub;
            }

            Collect(pluginContext, componentHub?.PluginManager?.GetAssociatedApplications(pluginContext) ?? []);
        }

        /// <summary>
        /// Collects the items of a plugin for the given applications - for a manager that decides
        /// the association itself. Collecting a plugin again for an application does nothing.
        /// </summary>
        /// <param name="pluginContext">The plugin.</param>
        /// <param name="applicationContexts">The applications.</param>
        /// <exception cref="InvalidOperationException">The catalog is bound per plugin.</exception>
        public void Collect(IPluginContext pluginContext, IEnumerable<IApplicationContext> applicationContexts)
        {
            ArgumentNullException.ThrowIfNull(pluginContext);
            Demand(PluginTypeBinding.Application);

            foreach (var applicationContext in (applicationContexts ?? []).Where(x => x is not null).Distinct())
            {
                CollectOffer(pluginContext, applicationContext, pluginContext.Assembly);
            }
        }

        /// <summary>
        /// Takes out what a plugin offered. Only the keys it still provides change: each falls back
        /// to the latest remaining offer of another plugin, or leaves.
        /// </summary>
        /// <param name="pluginContext">The plugin.</param>
        public void Withdraw(IPluginContext pluginContext)
        {
            if (pluginContext is not null)
            {
                WithdrawOffers(x => !x.Registered && ReferenceEquals(x.PluginContext, pluginContext));
            }
        }

        /// <summary>
        /// Takes out every item bound to an application, the registered ones included, because
        /// nothing can ask for them once the application is gone.
        /// </summary>
        /// <param name="applicationContext">The application.</param>
        public void Withdraw(IApplicationContext applicationContext)
        {
            if (applicationContext is not null)
            {
                WithdrawOffers(x => ReferenceEquals(x.ApplicationContext, applicationContext));
            }
        }

        /// <summary>
        /// Files an item that is no type of its own - one per configuration entry, a test's
        /// stand-in. It becomes the latest offer of its key and replaces an item registered under
        /// the same key before; it belongs to no plugin, no plugin removal takes it away, and the
        /// catalog never disposes it.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <exception cref="ArgumentException">The item has no key.</exception>
        /// <exception cref="InvalidOperationException">The catalog is bound per application.</exception>
        public void Register(TItem item)
        {
            Demand(PluginTypeBinding.Plugin);

            RegisterOffer(null, item);
        }

        /// <summary>
        /// Files an item that is no type of its own for an application, with the rules of
        /// <see cref="Register(TItem)"/>; it leaves with the application.
        /// </summary>
        /// <param name="applicationContext">The application.</param>
        /// <param name="item">The item.</param>
        /// <exception cref="ArgumentException">The item has no key.</exception>
        /// <exception cref="InvalidOperationException">The catalog is bound per plugin.</exception>
        public void Register(IApplicationContext applicationContext, TItem item)
        {
            ArgumentNullException.ThrowIfNull(applicationContext);
            Demand(PluginTypeBinding.Application);

            RegisterOffer(applicationContext, item);
        }

        /// <summary>
        /// Takes out the item registered under a key. A plugin offering the key takes over.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns><see langword="true"/> when an item was registered under the key.</returns>
        public bool Unregister(string key)
        {
            return Unregister(null, key);
        }

        /// <summary>
        /// Takes out the item registered under a key for an application.
        /// </summary>
        /// <param name="applicationContext">The application, or <see langword="null"/> for a catalog bound per plugin.</param>
        /// <param name="key">The key.</param>
        /// <returns><see langword="true"/> when an item was registered under the key.</returns>
        public bool Unregister(IApplicationContext applicationContext, string key)
        {
            var normalized = Normalize(key);

            if (normalized is null)
            {
                return false;
            }

            return WithdrawOffers(x => x.Registered
                && ReferenceEquals(x.ApplicationContext, applicationContext)
                && x.Items.ContainsKey(normalized)) > 0;
        }

        /// <summary>
        /// Returns the item in use under a key.
        /// </summary>
        /// <param name="key">The key. May be blank.</param>
        /// <returns>The item, or <see langword="null"/>.</returns>
        public TItem Find(string key)
        {
            return Find(null, key);
        }

        /// <summary>
        /// Returns the item in use under a key for an application.
        /// </summary>
        /// <param name="applicationContext">The application, or <see langword="null"/> for a catalog bound per plugin.</param>
        /// <param name="key">The key. May be blank.</param>
        /// <returns>The item, or <see langword="null"/>.</returns>
        public TItem Find(IApplicationContext applicationContext, string key)
        {
            var normalized = Normalize(key);

            if (normalized is null)
            {
                return null;
            }

            lock (_sync)
            {
                return _inUse.TryGetValue(new Slot(applicationContext, normalized), out var offer)
                    ? offer.Items[normalized]
                    : null;
            }
        }

        /// <summary>
        /// Answers whether an item is in use under a key.
        /// </summary>
        /// <param name="key">The key. May be blank.</param>
        /// <returns><see langword="true"/> when one is.</returns>
        public bool Contains(string key)
        {
            return Find(null, key) is not null;
        }

        /// <summary>
        /// Answers whether an item is in use under a key for an application.
        /// </summary>
        /// <param name="applicationContext">The application, or <see langword="null"/> for a catalog bound per plugin.</param>
        /// <param name="key">The key. May be blank.</param>
        /// <returns><see langword="true"/> when one is.</returns>
        public bool Contains(IApplicationContext applicationContext, string key)
        {
            return Find(applicationContext, key) is not null;
        }

        /// <summary>
        /// Returns the plugin whose offer is in use under a key - to tell a diagnostic which plugin
        /// provides an item, or which one shadows another.
        /// </summary>
        /// <param name="key">The key. May be blank.</param>
        /// <returns>The plugin, or <see langword="null"/> when nothing is in use under the key or the
        /// item was registered or collected from an assembly without a plugin.</returns>
        public IPluginContext OwnerOf(string key)
        {
            return OwnerOf(null, key);
        }

        /// <summary>
        /// Returns the plugin whose offer is in use under a key for an application.
        /// </summary>
        /// <param name="applicationContext">The application, or <see langword="null"/> for a catalog bound per plugin.</param>
        /// <param name="key">The key. May be blank.</param>
        /// <returns>The plugin, or <see langword="null"/>.</returns>
        public IPluginContext OwnerOf(IApplicationContext applicationContext, string key)
        {
            var normalized = Normalize(key);

            if (normalized is null)
            {
                return null;
            }

            lock (_sync)
            {
                return _inUse.TryGetValue(new Slot(applicationContext, normalized), out var offer)
                    ? offer.PluginContext
                    : null;
            }
        }

        /// <summary>
        /// Returns the items in use for an application, in the order their offers arrived.
        /// </summary>
        /// <param name="applicationContext">The application.</param>
        /// <returns>A copy of the items.</returns>
        public IReadOnlyList<TItem> GetItems(IApplicationContext applicationContext)
        {
            lock (_sync)
            {
                return Snapshot(x => ReferenceEquals(x.ApplicationContext, applicationContext));
            }
        }

        /// <summary>
        /// Stops following the host and takes out every item, raising <see cref="Removed"/> for the
        /// ones in use and disposing the ones it constructed. A disposed catalog collects nothing.
        /// </summary>
        public void Dispose()
        {
            IComponentHub componentHub;
            List<Change> changes;
            List<Offer> removed;

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                componentHub = _componentHub;

                removed = [.. _offers];
                _offers.Clear();
                changes = [.. _inUse.Select(x => new Change(x.Key, x.Value, null))];
                _inUse.Clear();

                if (changes.Count > 0)
                {
                    _version++;
                }
            }

            if (componentHub?.PluginManager is { } plugins)
            {
                plugins.AddPlugin -= OnAddPlugin;
                plugins.RemovePlugin -= OnRemovePlugin;
            }

            if (Binding == PluginTypeBinding.Application && componentHub?.ApplicationManager is { } applications)
            {
                applications.AddApplication -= OnAddApplication;
                applications.RemoveApplication -= OnRemoveApplication;
            }

            Raise(changes);
            Release(removed);
        }

        /// <summary>
        /// Refuses an operation that has no meaning for the catalog's binding, because answering it
        /// silently would file items where no lookup finds them.
        /// </summary>
        /// <param name="binding">The binding the operation needs.</param>
        private void Demand(PluginTypeBinding binding)
        {
            if (Binding != binding)
            {
                throw new InvalidOperationException($"The operation needs a catalog bound per {binding.ToString().ToLowerInvariant()}, this one is bound per {Binding.ToString().ToLowerInvariant()}.");
            }
        }

        /// <summary>
        /// Discovers and files the items of one plugin (or assembly) and application. The types
        /// are scanned and the items built outside the lock; an offer that arrived meanwhile wins
        /// and the duplicate is discarded.
        /// </summary>
        /// <param name="pluginContext">The plugin, or null for an assembly without one.</param>
        /// <param name="applicationContext">The application, or null for a catalog bound per plugin.</param>
        /// <param name="assembly">The assembly to scan.</param>
        private void CollectOffer(IPluginContext pluginContext, IApplicationContext applicationContext, Assembly assembly)
        {
            if (assembly is null)
            {
                return;
            }

            lock (_sync)
            {
                if (_disposed || _offers.Any(x => x.IsOfferOf(pluginContext, applicationContext, assembly)))
                {
                    return;
                }
            }

            var offer = new Offer(pluginContext, applicationContext, assembly, registered: false, Discover(pluginContext, applicationContext, assembly));
            List<Change> changes = null;

            lock (_sync)
            {
                if (!_disposed && !_offers.Any(x => x.IsOfferOf(pluginContext, applicationContext, assembly)))
                {
                    _offers.Add(offer);
                    changes = Resolve(offer.Items.Keys.Select(x => new Slot(applicationContext, x)));
                }
            }

            if (changes is null)
            {
                Release([offer]);

                return;
            }

            Raise(changes);
        }

        /// <summary>
        /// Files a registered item as the latest offer of its key, replacing an item registered
        /// under the same key before.
        /// </summary>
        /// <param name="applicationContext">The application, or null for a catalog bound per plugin.</param>
        /// <param name="item">The item.</param>
        private void RegisterOffer(IApplicationContext applicationContext, TItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            var key = Normalize(_keyOf(item)) ?? throw new ArgumentException("The item has no key.", nameof(item));
            var offer = new Offer(null, applicationContext, null, registered: true, new Dictionary<string, TItem>(StringComparer.OrdinalIgnoreCase) { [key] = item });
            List<Change> changes;

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _offers.RemoveAll(x => x.Registered && ReferenceEquals(x.ApplicationContext, applicationContext) && x.Items.ContainsKey(key));
                _offers.Add(offer);
                changes = Resolve([new Slot(applicationContext, key)]);
            }

            Raise(changes);
        }

        /// <summary>
        /// Takes out the offers a predicate selects and lets every key they touched fall back.
        /// </summary>
        /// <param name="predicate">Selects the offers.</param>
        /// <returns>The number of offers taken out.</returns>
        private int WithdrawOffers(Func<Offer, bool> predicate)
        {
            List<Offer> removed;
            List<Change> changes;

            lock (_sync)
            {
                if (_disposed)
                {
                    return 0;
                }

                removed = [.. _offers.Where(predicate)];

                if (removed.Count == 0)
                {
                    return 0;
                }

                foreach (var offer in removed)
                {
                    _offers.Remove(offer);
                }

                changes = Resolve(removed.SelectMany(x => x.Items.Keys.Select(y => new Slot(x.ApplicationContext, y))));
            }

            Raise(changes);
            Release(removed);

            return removed.Count;
        }

        /// <summary>
        /// Brings the items in use for the given slots in step with the offers: each slot takes the
        /// latest offer of its key. Must be called under the lock.
        /// </summary>
        /// <param name="slots">The slots whose offers changed.</param>
        /// <returns>The changes of the items in use, for the events.</returns>
        private List<Change> Resolve(IEnumerable<Slot> slots)
        {
            var changes = new List<Change>();

            foreach (var slot in slots.Distinct(SlotComparer.Instance))
            {
                _inUse.TryGetValue(slot, out var before);

                var after = _offers.LastOrDefault(x => ReferenceEquals(x.ApplicationContext, slot.ApplicationContext) && x.Items.ContainsKey(slot.Key));

                if (ReferenceEquals(before, after))
                {
                    continue;
                }

                if (after is null)
                {
                    _inUse.Remove(slot);
                }
                else
                {
                    _inUse[slot] = after;
                }

                changes.Add(new Change(slot, before, after));
            }

            if (changes.Count > 0)
            {
                _version++;
            }

            return changes;
        }

        /// <summary>
        /// Returns the items in use of the offers a predicate selects, in the order the offers
        /// arrived. Must be called under the lock.
        /// </summary>
        /// <param name="predicate">Selects the offers.</param>
        /// <returns>A copy of the items.</returns>
        private List<TItem> Snapshot(Func<Offer, bool> predicate)
        {
            var items = new List<TItem>();

            foreach (var offer in _offers.Where(predicate))
            {
                foreach (var (key, item) in offer.Items)
                {
                    if (_inUse.TryGetValue(new Slot(offer.ApplicationContext, key), out var inUse) && ReferenceEquals(inUse, offer))
                    {
                        items.Add(item);
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Raises the events of a set of changes, outside the lock, the leaving item before the
        /// arriving one, so a handler that mirrors the catalog never holds two items for one key.
        /// </summary>
        /// <param name="changes">The changes.</param>
        private void Raise(IEnumerable<Change> changes)
        {
            foreach (var change in changes)
            {
                if (change.Before is { } before)
                {
                    Removed?.Invoke(this, new PluginTypeCatalogEventArgs<TItem>(change.Slot.Key, before.Items[change.Slot.Key], before.PluginContext, before.ApplicationContext));
                }

                if (change.After is { } after)
                {
                    Added?.Invoke(this, new PluginTypeCatalogEventArgs<TItem>(change.Slot.Key, after.Items[change.Slot.Key], after.PluginContext, after.ApplicationContext));
                }
            }
        }

        /// <summary>
        /// Disposes the items of offers that left the catalog, where the catalog constructed them.
        /// </summary>
        /// <param name="offers">The offers.</param>
        private void Release(IEnumerable<Offer> offers)
        {
            foreach (var offer in offers.Where(x => !x.Registered))
            {
                foreach (var item in offer.Items.Values)
                {
                    Release(item);
                }
            }
        }

        /// <summary>
        /// Disposes an item the catalog constructed, keeping a faulty disposal from stopping the others.
        /// </summary>
        /// <param name="item">The item.</param>
        private void Release(TItem item)
        {
            if (item is not IDisposable disposable)
            {
                return;
            }

            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                _httpServerContext?.Log?.Exception(ex);
            }
        }

        /// <summary>
        /// Scans an assembly and builds an item of every type that follows the convention. A type
        /// that fails to load, to construct or to name its key is logged and skipped, so one
        /// defective item does not take the others of its plugin along.
        /// </summary>
        /// <param name="pluginContext">The plugin, or null.</param>
        /// <param name="applicationContext">The application, or null.</param>
        /// <param name="assembly">The assembly.</param>
        /// <returns>The items, keyed.</returns>
        private Dictionary<string, TItem> Discover(IPluginContext pluginContext, IApplicationContext applicationContext, Assembly assembly)
        {
            var items = new Dictionary<string, TItem>(StringComparer.OrdinalIgnoreCase);
            var source = pluginContext?.PluginId?.ToString() ?? assembly.GetName().Name;

            foreach (var type in TypesOf(assembly, source))
            {
                TItem item;

                try
                {
                    item = _factory is null
                        ? (TItem)Activator.CreateInstance(type)
                        : _factory(new PluginTypeOrigin(type, pluginContext, applicationContext));
                }
                catch (Exception ex)
                {
                    var cause = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
                    Warn("webexpress.webcore:plugintypecatalog.type_failed", type.FullName, source, cause.Message);

                    continue;
                }

                if (item is null)
                {
                    continue;
                }

                string key;

                try
                {
                    key = Normalize(_keyOf(item));
                }
                catch (Exception ex)
                {
                    Warn("webexpress.webcore:plugintypecatalog.type_failed", type.FullName, source, ex.Message);
                    Release(item);

                    continue;
                }

                if (key is null)
                {
                    Warn("webexpress.webcore:plugintypecatalog.key_missing", type.FullName, source);
                    Release(item);

                    continue;
                }

                if (!items.TryAdd(key, item))
                {
                    // two types of one plugin under one key: the plugin is defective, keep the first
                    Warn("webexpress.webcore:plugintypecatalog.key_duplicate", type.FullName, source, key);
                    Release(item);
                }
            }

            return items;
        }

        /// <summary>
        /// Returns the types of an assembly that follow the convention. An assembly that loads only
        /// in part yields the types that loaded.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <param name="source">The name the log gives the assembly.</param>
        /// <returns>The types.</returns>
        private IEnumerable<Type> TypesOf(Assembly assembly, string source)
        {
            Type[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                Warn("webexpress.webcore:plugintypecatalog.types_incomplete", source, ex.LoaderExceptions.FirstOrDefault(x => x is not null)?.Message ?? ex.Message);
                types = [.. ex.Types.Where(x => x is not null)];
            }
            catch (Exception ex)
            {
                Warn("webexpress.webcore:plugintypecatalog.types_incomplete", source, ex.Message);

                return [];
            }

            return types.Where(IsItemType).ToArray();
        }

        /// <summary>
        /// Answers whether a type follows the convention.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns><see langword="true"/> when it is an item type.</returns>
        private bool IsItemType(Type type)
        {
            return type.IsClass
                && type.IsPublic
                && type.IsSealed
                && !type.IsAbstract
                && !type.ContainsGenericParameters
                && _contract.IsAssignableFrom(type)
                && (_factory is not null || type.GetConstructor(Type.EmptyTypes) is not null);
        }

        /// <summary>
        /// Writes a warning to the host's log, when the catalog follows one.
        /// </summary>
        /// <param name="key">The i18n key of the message.</param>
        /// <param name="args">The arguments.</param>
        private void Warn(string key, params object[] args)
        {
            _httpServerContext?.Log?.Warning(I18N.Translate(key, args));
        }

        /// <summary>
        /// Trims a key and maps a blank one to null.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The normalized key, or null.</returns>
        private static string Normalize(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
        }

        /// <summary>
        /// Collects a plugin that arrived.
        /// </summary>
        /// <param name="sender">The plugin manager.</param>
        /// <param name="pluginContext">The plugin.</param>
        private void OnAddPlugin(object sender, IPluginContext pluginContext)
        {
            if (pluginContext is not null)
            {
                Collect(pluginContext);
            }
        }

        /// <summary>
        /// Takes out what a plugin that left offered.
        /// </summary>
        /// <param name="sender">The plugin manager.</param>
        /// <param name="pluginContext">The plugin.</param>
        private void OnRemovePlugin(object sender, IPluginContext pluginContext)
        {
            Withdraw(pluginContext);
        }

        /// <summary>
        /// Collects, for an application that arrived, the plugins already associated with it.
        /// </summary>
        /// <param name="sender">The application manager.</param>
        /// <param name="applicationContext">The application.</param>
        private void OnAddApplication(object sender, IApplicationContext applicationContext)
        {
            if (applicationContext is null)
            {
                return;
            }

            IComponentHub componentHub;

            lock (_sync)
            {
                componentHub = _componentHub;
            }

            foreach (var plugin in componentHub?.PluginManager?.GetPlugins(applicationContext)?.ToArray() ?? [])
            {
                Collect(plugin, [applicationContext]);
            }
        }

        /// <summary>
        /// Takes out the items of an application that left.
        /// </summary>
        /// <param name="sender">The application manager.</param>
        /// <param name="applicationContext">The application.</param>
        private void OnRemoveApplication(object sender, IApplicationContext applicationContext)
        {
            Withdraw(applicationContext);
        }

        /// <summary>
        /// What one plugin (or assembly, or registration) offers for one application.
        /// </summary>
        private sealed class Offer
        {
            /// <summary>
            /// Gets the plugin, or null for a registration or an assembly without a plugin.
            /// </summary>
            public IPluginContext PluginContext { get; }

            /// <summary>
            /// Gets the application, or null for a catalog bound per plugin.
            /// </summary>
            public IApplicationContext ApplicationContext { get; }

            /// <summary>
            /// Gets the scanned assembly, or null for a registration.
            /// </summary>
            public Assembly Assembly { get; }

            /// <summary>
            /// Gets whether the item was given to Register rather than discovered.
            /// </summary>
            public bool Registered { get; }

            /// <summary>
            /// Gets the items, keyed.
            /// </summary>
            public IReadOnlyDictionary<string, TItem> Items { get; }

            /// <summary>
            /// Captures an offer.
            /// </summary>
            /// <param name="pluginContext">The plugin.</param>
            /// <param name="applicationContext">The application.</param>
            /// <param name="assembly">The assembly.</param>
            /// <param name="registered">Whether it is a registration.</param>
            /// <param name="items">The items.</param>
            public Offer(IPluginContext pluginContext, IApplicationContext applicationContext, Assembly assembly, bool registered, IReadOnlyDictionary<string, TItem> items)
            {
                PluginContext = pluginContext;
                ApplicationContext = applicationContext;
                Assembly = assembly;
                Registered = registered;
                Items = items;
            }

            /// <summary>
            /// Answers whether this is the discovered offer of a plugin (or assembly) for an application.
            /// </summary>
            /// <param name="pluginContext">The plugin.</param>
            /// <param name="applicationContext">The application.</param>
            /// <param name="assembly">The assembly.</param>
            /// <returns><see langword="true"/> when it is.</returns>
            public bool IsOfferOf(IPluginContext pluginContext, IApplicationContext applicationContext, Assembly assembly)
            {
                return !Registered
                    && ReferenceEquals(ApplicationContext, applicationContext)
                    && (pluginContext is null
                        ? PluginContext is null && ReferenceEquals(Assembly, assembly)
                        : ReferenceEquals(PluginContext, pluginContext));
            }
        }

        /// <summary>
        /// A key of an application (null for a catalog bound per plugin).
        /// </summary>
        /// <param name="ApplicationContext">The application.</param>
        /// <param name="Key">The normalized key.</param>
        private readonly record struct Slot(IApplicationContext ApplicationContext, string Key);

        /// <summary>
        /// A change of the offer in use for a slot.
        /// </summary>
        /// <param name="Slot">The slot.</param>
        /// <param name="Before">The offer in use before, or null.</param>
        /// <param name="After">The offer in use after, or null.</param>
        private readonly record struct Change(Slot Slot, Offer Before, Offer After);

        /// <summary>
        /// Compares slots by the identity of their application and the key without case, because an
        /// application context is identified by reference throughout the framework.
        /// </summary>
        private sealed class SlotComparer : IEqualityComparer<Slot>
        {
            /// <summary>
            /// Gets the comparer.
            /// </summary>
            public static SlotComparer Instance { get; } = new();

            /// <inheritdoc/>
            public bool Equals(Slot x, Slot y)
            {
                return ReferenceEquals(x.ApplicationContext, y.ApplicationContext)
                    && StringComparer.OrdinalIgnoreCase.Equals(x.Key, y.Key);
            }

            /// <inheritdoc/>
            public int GetHashCode(Slot obj)
            {
                return HashCode.Combine(RuntimeHelpers.GetHashCode(obj.ApplicationContext), StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Key ?? string.Empty));
            }
        }
    }
}
