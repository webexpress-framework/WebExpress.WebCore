
using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.WebIdentity
{
    /// <summary>
    /// A component that defines a single permission — an individual right that can be granted or
    /// checked. Permissions are the smallest unit of access control and are combined into policies
    /// (<see cref="IIdentityPolicy"/>).
    /// </summary>
    public interface IIdentityPermission : IComponent
    {
    }
}
