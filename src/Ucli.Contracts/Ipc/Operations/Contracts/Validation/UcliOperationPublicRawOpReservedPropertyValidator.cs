using System.Reflection;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliOperationPublicRawOpReservedPropertyValidator
{
    private static readonly Type JsonElementType = typeof(JsonElement);

    public static bool TryValidate (
        Type contractType,
        out string errorMessage)
    {
        return TryValidate(contractType, "args", new HashSet<Type>(), out errorMessage);
    }

    private static bool TryValidate (
        Type contractType,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        var actualType = Nullable.GetUnderlyingType(contractType) ?? contractType;
        if (IsAliasValueType(actualType, path, out errorMessage))
        {
            return false;
        }

        return TryValidateBranch(actualType, path, visitedTypes, out errorMessage);
    }

    private static bool TryValidateBranch (
        Type actualType,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        if (UcliOperationContractTypeFacts.IsScalar(actualType) || actualType == JsonElementType || actualType == typeof(object))
        {
            errorMessage = string.Empty;
            return true;
        }

        if (UcliOperationContractTypeFacts.TryGetArrayElementType(actualType, out var elementType))
        {
            return TryValidate(elementType!, path + "[]", visitedTypes, out errorMessage);
        }

        return TryValidateObject(actualType, path, visitedTypes, out errorMessage);
    }

    private static bool TryValidateObject (
        Type actualType,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (!visitedTypes.Add(actualType))
        {
            return true;
        }

        foreach (var property in UcliOperationContractReflection.GetContractProperties(actualType))
        {
            if (!TryValidateProperty(property, path, visitedTypes, out errorMessage))
            {
                visitedTypes.Remove(actualType);
                return false;
            }
        }

        visitedTypes.Remove(actualType);
        return true;
    }

    private static bool TryValidateProperty (
        PropertyInfo property,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        var propertyName = UcliOperationContractReflection.GetJsonPropertyName(property);
        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (UcliRequestLocalAliasContractPolicy.IsInternalRequestLocalAliasBranchProperty(property))
        {
            errorMessage = string.Empty;
            return true;
        }

        return TryValidatePublicProperty(property, propertyType, $"{path}.{propertyName}", visitedTypes, out errorMessage);
    }

    private static bool TryValidatePublicProperty (
        PropertyInfo property,
        Type propertyType,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        if (IsAliasValueType(propertyType, path, out errorMessage)
            || IsAliasPropertyName(path, out errorMessage))
        {
            return false;
        }

        return ShouldVisit(property)
            ? TryValidate(property.PropertyType, path, visitedTypes, out errorMessage)
            : Succeed(out errorMessage);
    }

    private static bool IsAliasValueType (
        Type type,
        string path,
        out string errorMessage)
    {
        if (UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasValueType(type))
        {
            errorMessage = $"Operation contract property '{path}' uses internal request-local alias type '{type.Name}'.";
            return true;
        }

        errorMessage = string.Empty;
        return false;
    }

    private static bool IsAliasPropertyName (
        string path,
        out string errorMessage)
    {
        if (path.EndsWith("." + UcliOperationContractPropertyNames.Alias, StringComparison.Ordinal))
        {
            errorMessage = $"Operation contract property '{path}' uses reserved public raw-op property name '{UcliOperationContractPropertyNames.Alias}'.";
            return true;
        }

        errorMessage = string.Empty;
        return false;
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
