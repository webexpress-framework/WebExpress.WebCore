using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebUri;

namespace WebExpress.WebCore.WebSitemap.Model
{
    /// <summary>
    /// One node in the sitemap — the tree of all routes the server can serve. Each node represents a
    /// path segment, optionally the endpoint reachable there, and links to its parent and child
    /// nodes. Routing a request means walking this tree segment by segment.
    /// </summary>
    public class SitemapNode
    {
        /// <summary>
        /// Gets the node path segment.
        /// </summary>
        public IUriPathSegment PathSegment { get; internal set; }

        /// <summary>
        /// Gets the context of the endpoint.
        /// </summary>
        public IEndpointContext EndpointContext { get; internal set; }

        /// <summary>
        /// Gets the child nodes.
        /// </summary>
        public ICollection<SitemapNode> Children { get; private set; } = [];

        /// <summary>
        /// Gets the parent node.
        /// </summary>
        public SitemapNode Parent { get; internal set; }

        /// <summary>
        /// Gets the root.
        /// </summary>
        public SitemapNode Root
        {
            get
            {
                if (IsRoot)
                {
                    return this;
                }

                var parent = Parent;
                while (!parent.IsRoot)
                {
                    parent = parent.Parent;
                }

                return parent;
            }
        }

        /// <summary>
        /// Checks whether the node is the root.
        /// </summary>
        /// <returns>true if root, otherwise false.</returns>
        public bool IsRoot => Parent is null;

        /// <summary>
        /// Checks whether the node is a leaf.
        /// </summary>
        /// <returns>true if a leaf, otherwise false.</returns>
        public bool IsLeaf => Children.Count == 0;

        /// <summary>
        /// Gets the path.
        /// </summary>
        /// <returns>The path.</returns>
        public ICollection<SitemapNode> Path
        {
            get
            {
                var list = new List<SitemapNode>
                {
                    this
                };

                var parent = Parent;
                while (parent is not null)
                {
                    list.Add(parent);

                    parent = parent.Parent;
                }

                list.Reverse();

                return list;
            }
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public SitemapNode()
        {

        }

        /// <summary>
        /// Passes through the tree in pre order.
        /// </summary>
        /// <returns>The tree as a list.</returns>
        public IEnumerable<SitemapNode> GetPreOrder()
        {
            var list = new List<SitemapNode>
            {
                this
            };

            foreach (var child in Children.OrderBy(x => x.PathSegment?.Id))
            {
                list.AddRange(child.GetPreOrder());
            }

            return list;
        }

        /// <summary>
        /// Creates a copy of the sitemap node.
        /// </summary>
        /// <returns>The copy of the sitemap node.</returns>
        public SitemapNode Copy()
        {
            var node = new SitemapNode()
            {
                PathSegment = PathSegment,
                EndpointContext = EndpointContext,
                Parent = Parent,
                Children = Children.Select(x => x.Copy()).ToList()
            };

            return node;
        }

        /// <summary>
        /// Convert to string.
        /// </summary>
        /// <returns>The sitemap node in its string representation.</returns>
        public override string ToString()
        {
            return Path.FirstOrDefault()?.PathSegment + string.Join
            (
                "/",
                Path.Where(x => x.PathSegment is not UriPathSegmentRoot)
                .Select(x => x.PathSegment?.ToString())
           );
        }
    }
}
