using System.ComponentModel;
using System.Reflection;

namespace AduosSyncServices.Contracts.Extensions
{
    public static class EnumDescriptionExtensions
    {
        /// <summary>
        /// Returns the enum member's [Description] text (e.g. a Polish display name for UI),
        /// falling back to the raw member name if none is set. Purely a display concern -
        /// never use this for values sent to an external API (JSON serialization of these
        /// enums is untouched by [Description] and still uses the member's own name).
        /// </summary>
        public static string GetDescription(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? value.ToString();
        }
    }
}
