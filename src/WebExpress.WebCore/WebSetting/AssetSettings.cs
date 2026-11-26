namespace WebExpress.WebCore.WebSetting
{
    /// <summary>
    /// Optional settings of the delivery of scripts and stylesheets. The whole block and every
    /// property in it is optional: a value left unset keeps the built-in default.
    /// </summary>
    public sealed class AssetSettings
    {
        /// <summary>
        /// Whether the files of an include are joined into one bundle per type and linked as one.
        /// Defaults to <c>true</c>: a page links several hundred files, and loading each on its
        /// own costs a request apiece. Switched off, every file is linked on its own, which keeps
        /// the file and line of a script error readable while developing.
        /// </summary>
        public bool Bundle { get; set; } = true;
    }
}
