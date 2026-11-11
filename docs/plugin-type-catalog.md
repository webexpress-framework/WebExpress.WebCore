![WebExpress](https://raw.githubusercontent.com/webexpress-framework/.github/main/docs/assets/img/banner.png)

# Plugin type catalog

An application built on WebExpress often has extension points of its own: export formats, automation actions, integrations with other systems. Each of them asks the same question the framework asks for its pages, fragments and health checks: *which plugin contributes which implementation?* `PluginTypeCatalog<TItem>` (namespace `WebExpress.WebCore.WebPlugin`) answers it, so a manager does not have to scan the plugin assemblies, follow the plugin and application events and keep track of who provides what by hand.

The catalog is a building block, not a manager and not a base class. A manager holds one, or several, as a field and delegates to it.

## When to use it

Use a catalog when the implementations of an extension point are **types in plugins** that should be found without any registration: a plugin ships a `public sealed` class, and the extension point knows it once the plugin is loaded. Do not use it for UI building blocks - fragments carry a section and a scope and are rendered by the `FragmentManager` - nor for configuration entries, which are no types (for those, see `Register` below).

## The convention

A type of a plugin's assembly is an item when it is

- a class that is `public` at top level - a nested type is not, because `Type.IsPublic` is false for it,
- `sealed`, not abstract and not an open generic,
- assignable to the contract (by default `TItem`),
- and, without a factory, has a public parameterless constructor.

A type that misses any of these is skipped **silently**, the way a page or a fragment that does not follow its convention is. A plugin should therefore have a test that every item it ships follows the convention - otherwise an item is simply missing in a running host.

A type that is found but cannot be used - its constructor throws, it names no key, a second type of the same plugin uses the same key, or the assembly loads only in part - is written to the server log as a warning and skipped. The other items of the plugin are still collected.

## Keys and ownership

Every item has a key, answered by the function the catalog is created with. Keys are trimmed and compared without case.

Several plugins may offer an item under the same key - an add-on replacing a standard implementation, for instance. The catalog keeps every offer in the order the plugins arrived, and the item **in use** under a key is the latest offer. When a plugin leaves, it takes only what it offered:

- a key it still provides falls back to the latest remaining offer of another plugin, and is removed only when nobody offers it any more;
- a key another plugin took over is left alone.

`Register(item)` files an item that is no type of its own - one per configuration entry, or a stand-in in a test. It competes like an offer (the latest offer wins), but it belongs to no plugin: no plugin removal takes it away, only `Unregister(key)` does, and the catalog never disposes it. An item the catalog constructed itself is disposed when it leaves the catalog, if it implements `IDisposable`.

## Following a host

`Attach(componentHub, httpServerContext)` does both passes a manager needs, because managers are built before all plugins have arrived:

1. it collects the plugins already loaded (`IPluginManager.Plugins`), and
2. it subscribes to `AddPlugin` and `RemovePlugin` for the ones that come and go later.

Collecting a plugin twice does nothing, so the two passes never double an item. `Dispose()` unsubscribes, raises `Removed` for every item in use and disposes the items it built. Without a plugin manager - in a unit test - `Collect(assembly)` collects one assembly; its items belong to no plugin.

## Reading and change signalling

- `Find(key)`, `Contains(key)` and `OwnerOf(key)` answer the item in use under a key and the plugin that provides it (`null` for a registered item).
- `Items` is a snapshot of the items in use, in the order their offers arrived.
- `Version` moves whenever an item comes, goes or is replaced - a cached picker or menu compares it instead of the items.
- `Added` and `Removed` report each change, carrying the key, the item and the plugin. The leaving item is reported before the arriving one, and both events are raised after the catalog released its lock, so a handler may call back into it.

The catalog is thread-safe; types are scanned and items constructed outside its lock.

## Items bound to an application

The framework's own extension points are scoped to an application: a health check, for example, is bound once per plugin and per application the plugin is associated with. A catalog created with `PluginTypeBinding.Application` does the same. It also follows `AddApplication` and `RemoveApplication`, keeps a key per application, and offers `Find(application, key)`, `GetItems(application)`, `Collect(plugin, applications)`, `Withdraw(application)` and `Register(application, item)`. A catalog bound per plugin - the default, and the right choice for most application extension points - pays nothing for this.

A factory builds items that need their context, or that wrap the discovered type instead of being it. It receives a `PluginTypeOrigin` (type, plugin, application); answering `null` declines the type. The `HealthManager` uses one to wrap each `IHealth` type in a binding that activates the component only when it is checked.

## Example

An application defines an extension point - every `public sealed IExportFormat` of any plugin:

```csharp
public interface IExportFormat
{
    string Key { get; }

    byte[] Export(IEnumerable<object> rows);
}

public sealed class ExportFormatManager : IExportFormatManager
{
    private readonly PluginTypeCatalog<IExportFormat> _formats = new(x => x.Key);

    private ExportFormatManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
    {
        _formats.Attach(componentHub, httpServerContext);
    }

    public IReadOnlyList<IExportFormat> Formats => _formats.Items;

    public IExportFormat Get(string key) => _formats.Find(key);

    public void Dispose() => _formats.Dispose();
}
```

A plugin contributes a format by shipping a class - nothing else:

```csharp
public sealed class CsvExportFormat : IExportFormat
{
    public string Key => "csv";

    public byte[] Export(IEnumerable<object> rows) => ...;
}
```

And it tests that its items are found:

```csharp
[Fact]
public void EveryExportFormatIsFindable()
{
    using var catalog = new PluginTypeCatalog<IExportFormat>(x => x.Key);
    catalog.Collect(typeof(CsvExportFormat).Assembly);

    Assert.NotNull(catalog.Find("csv"));
}
```

## Candidates in the framework

The `HealthManager` discovers its checks through a catalog bound per application. The other managers that scan plugin assemblies still carry their own discovery and are candidates for the same move: `MetricsManager`, `JobManager`, `ThemeManager`, `SettingPageManager`, `FragmentManager`, `PageManager`, `RestApiManager`, `IncludeManager`, `EventManager`, `SocketManager` and `StatusPageManager`. Those that file their items by attributes and routes rather than by a key (pages, REST APIs) need more than a catalog and may keep their own bookkeeping.
