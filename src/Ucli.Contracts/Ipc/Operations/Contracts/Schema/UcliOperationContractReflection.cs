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
                && !IsAlwaysJsonIgnored(property))
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

    public static UcliInputConstraintAttribute[] GetInputConstraintAttributes (PropertyInfo property)
    {
        var actualType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var typeAttributes = actualType.GetCustomAttributes<UcliInputConstraintAttribute>().ToArray();
        var propertyAttributes = property.GetCustomAttributes<UcliInputConstraintAttribute>().ToArray();
        var attributes = new UcliInputConstraintAttribute[typeAttributes.Length + propertyAttributes.Length];
        Array.Copy(typeAttributes, attributes, typeAttributes.Length);
        Array.Copy(propertyAttributes, 0, attributes, typeAttributes.Length, propertyAttributes.Length);
        return attributes;
    }

    private static bool IsAlwaysJsonIgnored (PropertyInfo property)
    {
        var jsonIgnore = property.GetCustomAttribute<JsonIgnoreAttribute>();
        return jsonIgnore != null && jsonIgnore.Condition == JsonIgnoreCondition.Always;
    }
}
