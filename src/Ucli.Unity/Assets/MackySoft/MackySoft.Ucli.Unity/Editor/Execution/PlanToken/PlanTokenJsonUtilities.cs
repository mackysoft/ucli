using System.Text.Json;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Provides shared JSON utility methods for plan-token processing. </summary>
    internal static class PlanTokenJsonUtilities
    {
        /// <summary> Attempts to read one optional string property from JSON object. </summary>
        /// <param name="jsonObject"> The JSON object. </param>
        /// <param name="propertyName"> The property name. </param>
        /// <returns> The string value when present and valid; otherwise <see langword="null" />. </returns>
        public static string? TryReadString (
            JsonElement jsonObject,
            string propertyName)
        {
            if (!jsonObject.TryGetProperty(propertyName, out var valueElement)
                || valueElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var value = valueElement.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }
    }
}
