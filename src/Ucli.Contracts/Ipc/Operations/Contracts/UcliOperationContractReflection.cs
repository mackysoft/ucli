using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliOperationContractReflection
{
    public static IReadOnlyList<PropertyInfo> GetSchemaProperties (Type contractType)
    {
        return contractType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property =>
                property.GetMethod != null
                && property.GetIndexParameters().Length == 0
                && !IsAlwaysJsonIgnored(property)
                && property.GetCustomAttribute<UcliSchemaIgnoreAttribute>() == null)
            .OrderBy(static property => property.MetadataToken)
            .ToArray();
    }

    public static string GetJsonPropertyName (PropertyInfo property)
    {
        var jsonPropertyName = property.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (jsonPropertyName != null)
        {
            return jsonPropertyName.Name;
        }

        return JsonNamingPolicy.CamelCase.ConvertName(property.Name);
    }

    private static bool IsAlwaysJsonIgnored (PropertyInfo property)
    {
        var jsonIgnore = property.GetCustomAttribute<JsonIgnoreAttribute>();
        return jsonIgnore != null && jsonIgnore.Condition == JsonIgnoreCondition.Always;
    }
}
