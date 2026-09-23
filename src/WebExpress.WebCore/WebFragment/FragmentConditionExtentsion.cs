using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebMessage;

namespace WebExpress.WebCore.WebFragment
{
    /// <summary>
    /// Provides extension methods for checking conditions.
    /// </summary>
    public static class FragmentConditionExtentsion
    {
        /// <summary>
        /// Checks if all conditions in the collection are fulfilled for the given request.
        /// </summary>
        /// <param name="conditions">The collection of conditions to check.</param>
        /// <param name="request">The request to evaluate the conditions against.</param>
        /// <returns>True if all conditions are fulfilled; otherwise, false.</returns>
        public static bool Check(this IEnumerable<ICondition> conditions, IRequest request)
        {
            foreach (var condition in conditions)
            {
                if (!condition.Fulfillment(request))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Decides whether a fragment may appear for the given request. Besides the conditions, the
        /// identity must satisfy the fragment's policies, so a link to a protected page is only shown
        /// to those the server would serve the page to.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment to check.</param>
        /// <param name="request">The request whose identity and state are evaluated.</param>
        /// <returns>True if the conditions are fulfilled and the identity satisfies all policies; otherwise, false.</returns>
        public static bool Check(this IFragmentContext fragmentContext, IRequest request)
        {
            if (!(fragmentContext?.Conditions ?? []).Check(request))
            {
                return false;
            }

            var policies = fragmentContext?.Policies ?? [];

            if (!policies.Any())
            {
                return true;
            }

            var identityManager = WebEx.ComponentHub?.IdentityManager;
            var identity = identityManager?.GetCurrentIdentity(request);

            // without an identity manager no policy can be verified, so the fragment stays hidden
            return identityManager is not null && policies.All(x => identityManager.CheckAccess(identity, x));
        }
    }
}
