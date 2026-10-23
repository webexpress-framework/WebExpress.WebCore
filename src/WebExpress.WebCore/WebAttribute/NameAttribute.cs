using System;

namespace WebExpress.WebCore.WebAttribute
{
    /// <summary>
    /// Attribute to assign a name to a class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class NameAttribute : Attribute, IPluginAttribute, IApplicationAttribute, ISettingCategoryAttribute, ISettingGroupAttribute, IThemeAttribute, IJobAttribute
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="name">The name.</param>

        public NameAttribute(string name)
        {

        }
    }
}
