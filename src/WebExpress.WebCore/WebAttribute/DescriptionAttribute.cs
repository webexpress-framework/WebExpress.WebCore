using System;

namespace WebExpress.WebCore.WebAttribute
{
    /// <summary>
    /// Attribute to provide a description for a class.
    /// Implements <see cref="System.Attribute"/>, <see cref="IPluginAttribute"/>, and <see cref="IApplicationAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class DescriptionAttribute : Attribute, IPluginAttribute, IApplicationAttribute, ISettingCategoryAttribute, ISettingGroupAttribute, IThemeAttribute, IStatusPageAttribute, IJobAttribute
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="description">The description.</param>
        public DescriptionAttribute(string description)
        {

        }
    }
}
