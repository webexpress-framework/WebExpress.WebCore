using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// A component that defines an access-control policy: a named rule (built from permissions) that
    /// decides whether the current identity is allowed to do something. Plugins provide policies and
    /// the framework evaluates them when guarding pages, resources, or actions.
    /// </summary>
    public interface IIdentityPolicy : IComponent
    {
    }
}
