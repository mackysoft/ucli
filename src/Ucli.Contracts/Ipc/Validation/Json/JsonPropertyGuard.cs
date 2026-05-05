using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Provides helper functions that validate unknown-property contracts for JSON objects. </summary>
internal static class JsonPropertyGuard
{
    /// <summary> Finds the first unknown property in a JSON object. </summary>
    /// <param name="jsonObject"> The source JSON object. </param>
    /// <param name="allowedProperties"> The allowed property-name set. </param>
    /// <returns> The unknown property name, or <see langword="null" /> when all properties are allowed. </returns>
    public static string? FindUnknownProperty (
        JsonElement jsonObject,
        ISet<string> allowedProperties)
    {
        foreach (var property in jsonObject.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
            {
                return property.Name;
            }
        }

        return null;
    }
}
