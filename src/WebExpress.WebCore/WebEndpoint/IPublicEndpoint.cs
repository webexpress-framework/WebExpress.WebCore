namespace WebExpress.WebCore.WebEndpoint
{
    /// <summary>
    /// Marks an endpoint class - a page, a REST API, a resource or a socket - that answers callers
    /// who are not signed in, such as a login page or the API it signs in through.
    /// </summary>
    /// <remarks>
    /// An endpoint without a policy requires a signed-in caller: access is denied unless it is
    /// granted, since an endpoint that forgot its policy must not become public. A public endpoint
    /// says so, either with <c>[Policy&lt;PublicAccessPolicy&gt;]</c> on its class or by
    /// implementing this interface. The interface is inherited, which is what a base class such as
    /// the login page of an application template needs: attributes are read from the concrete
    /// class only.
    /// </remarks>
    public interface IPublicEndpoint
    {
    }
}
