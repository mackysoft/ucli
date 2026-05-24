using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliOperationRequestLocalAliasValueValidator
{
    private static readonly Type JsonElementType = typeof(JsonElement);

    public static bool TryValidate (
        object? value,
        Type contractType,
        out string errorMessage)
    {
        return TryValidateValue(value, contractType, "args", new HashSet<Type>(), out errorMessage);
    }

    private static bool TryValidateValue (
        object? value,
        Type contractType,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        var actualType = Nullable.GetUnderlyingType(contractType) ?? contractType;
        if (!UcliOperationContractTypeFacts.HasValue(value))
        {
            return true;
        }

        if (IsAliasValue(actualType, value))
        {
            errorMessage = $"Operation '{path}' cannot use request-local alias references in public op steps.";
            return false;
        }

        return TryValidateBranch(value, actualType, path, visitedTypes, out errorMessage);
    }

    private static bool TryValidateBranch (
        object? value,
        Type actualType,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        if (UcliOperationContractTypeFacts.TryGetArrayElementType(actualType, out var elementType))
        {
            return TryValidateArray(value!, elementType!, path, visitedTypes, out errorMessage);
        }

        if (UcliOperationContractTypeFacts.IsScalar(actualType) || actualType == JsonElementType || actualType == typeof(object))
        {
            errorMessage = string.Empty;
            return true;
        }

        return TryValidateObject(value, actualType, path, visitedTypes, out errorMessage);
    }

    private static bool TryValidateObject (
        object? value,
        Type contractType,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (value == null || !visitedTypes.Add(contractType))
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
        object owner,
        PropertyInfo property,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        var propertyValue = property.GetValue(owner);
        var propertyName = UcliOperationContractReflection.GetJsonPropertyName(property);
        if (UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasPropertyName(propertyName))
        {
            return TryValidateAliasProperty(propertyValue, path, propertyName, out errorMessage);
        }

        if (!ShouldVisit(property, propertyValue))
        {
            errorMessage = string.Empty;
            return true;
        }

        return TryValidateValue(propertyValue, property.PropertyType, $"{path}.{propertyName}", visitedTypes, out errorMessage);
    }

    private static bool TryValidateAliasProperty (
        object? propertyValue,
        string path,
        string propertyName,
        out string errorMessage)
    {
        if (UcliOperationContractTypeFacts.HasValue(propertyValue))
        {
            errorMessage = $"Operation '{path}.{propertyName}' cannot use reserved request-local alias property '{UcliOperationContractPropertyNames.Alias}' in public op steps.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateArray (
        object value,
        Type elementType,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (value is not IEnumerable enumerable)
        {
            return true;
        }

        var index = 0;
        foreach (var item in enumerable)
        {
            if (!TryValidateValue(item, elementType, $"{path}[{index.ToString(CultureInfo.InvariantCulture)}]", visitedTypes, out errorMessage))
            {
                return false;
            }

            index++;
        }

        return true;
    }

    private static bool IsAliasValue (
        Type actualType,
        object? value)
    {
        return UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasValueType(actualType)
            || (value != null && UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasValueType(value.GetType()));
    }

    private static bool ShouldVisit (
        PropertyInfo property,
        object? propertyValue)
    {
        return UcliOperationContractTypeFacts.HasValue(propertyValue)
            && property.GetCustomAttribute<UcliJsonAnyValueAttribute>() == null;
    }
}
