using System.Globalization;
using System.Reflection;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliOperationRequestLocalAliasJsonValidator
{
    private static readonly Type JsonElementType = typeof(JsonElement);

    public static bool TryValidate (
        JsonElement value,
        Type contractType,
        out string errorMessage)
    {
        return TryValidateValue(value, contractType, "args", new HashSet<Type>(), out errorMessage);
    }

    private static bool TryValidateValue (
        JsonElement value,
        Type contractType,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        var actualType = Nullable.GetUnderlyingType(contractType) ?? contractType;
        if (UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasValueType(actualType))
        {
            errorMessage = $"Operation '{path}' cannot use request-local alias references in public op steps.";
            return false;
        }

        return TryValidateBranch(value, actualType, path, visitedTypes, out errorMessage);
    }

    private static bool TryValidateBranch (
        JsonElement value,
        Type actualType,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        if (UcliOperationContractTypeFacts.TryGetArrayElementType(actualType, out var elementType))
        {
            return TryValidateArray(value, elementType!, path, visitedTypes, out errorMessage);
        }

        if (UcliOperationContractTypeFacts.IsScalar(actualType) || actualType == JsonElementType || actualType == typeof(object))
        {
            errorMessage = string.Empty;
            return true;
        }

        return TryValidateObject(value, actualType, path, visitedTypes, out errorMessage);
    }

    private static bool TryValidateObject (
        JsonElement value,
        Type contractType,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (value.ValueKind != JsonValueKind.Object || !visitedTypes.Add(contractType))
        {
            return true;
        }

        foreach (var property in UcliOperationContractReflection.GetContractProperties(contractType))
        {
            if (!TryValidateProperty(value, property, path, visitedTypes, out errorMessage))
            {
                visitedTypes.Remove(contractType);
                return false;
            }
        }

        visitedTypes.Remove(contractType);
        return true;
    }

    private static bool TryValidateProperty (
        JsonElement owner,
        PropertyInfo property,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        var propertyName = UcliOperationContractReflection.GetJsonPropertyName(property);
        if (!owner.TryGetProperty(propertyName, out var propertyValue))
        {
            errorMessage = string.Empty;
            return true;
        }

        if (UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasPropertyName(propertyName))
        {
            errorMessage = $"Operation '{path}.{propertyName}' cannot use reserved request-local alias property '{UcliOperationContractPropertyNames.Alias}' in public op steps.";
            return false;
        }

        return ShouldVisit(property)
            ? TryValidateValue(propertyValue, property.PropertyType, $"{path}.{propertyName}", visitedTypes, out errorMessage)
            : Succeed(out errorMessage);
    }

    private static bool TryValidateArray (
        JsonElement value,
        Type elementType,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (value.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (!TryValidateValue(item, elementType, $"{path}[{index.ToString(CultureInfo.InvariantCulture)}]", visitedTypes, out errorMessage))
            {
                return false;
            }

            index++;
        }

        return true;
    }

    private static bool ShouldVisit (PropertyInfo property)
    {
        return property.GetCustomAttribute<UcliJsonAnyValueAttribute>() == null;
    }

    private static bool Succeed (out string errorMessage)
    {
        errorMessage = string.Empty;
        return true;
    }
}
