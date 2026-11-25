using System;
using WebExpress.WebCore.WebEndpoint;

namespace WebExpress.WebCore.WebAsset.Model
{
    /// <summary>
    /// A assat element that contains meta information about a asset.
    /// </summary>
    internal class AssetItem : IDisposable
    {
        private readonly IAssetManager _assetManager;

        /// <summary>
        /// Gets or sets the type of asset.
        /// </summary>
        public Type AssetClass { get; set; }

        /// <summary>
        /// Gets or sets the instance of the asset, if the asset is cached, otherwise null.
        /// </summary>
        public IAsset Instance { get; set; }

        /// <summary>
        /// Gets the asset context.
        /// </summary>
        public IAssetContext AssetContext { get; internal set; }

        /// <summary>
        /// Gets the route of the same file with the hash of its content in the name, or null
        /// when the file has no versioned address.
        /// </summary>
        public IRoute VersionedRoute { get; internal set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="assetManager">The asset manager.</param>
        internal AssetItem(IAssetManager assetManager)
        {
            _assetManager = assetManager;
        }

        /// <summary>
        /// Performs application-specific tasks related to sharing, returning, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {

        }

        /// <summary>
        /// Convert the asset element to a string.
        /// </summary>
        /// <returns>The asset element in its string representation.</returns>
        public override string ToString()
        {
            return $"Asset: '{AssetContext?.EndpointId}'";
        }
    }
}
