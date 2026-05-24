using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Validates deserialized operation contract objects against structural uCLI contract attributes. </summary>
public static class UcliOperationContractValidator
{
    private static readonly Type JsonElementType = typeof(JsonElement);

    /// <summary> Validates one deserialized operation contract object. </summary>
    /// <param name="value"> The contract object value. </param>
    /// <param name="contractType"> The contract type that defines structural contract attributes. </param>
    /// <param name="errorMessage"> The validation error message when validation fails. </param>
    /// <returns> <see langword="true" /> when the value satisfies the contract; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="contractType" /> is <see langword="null" />. </exception>
    public static bool TryValidate (
        object? value,
        Type contractType,
        out string errorMessage)
    {
        if (contractType == null)
        {
            throw new ArgumentNullException(nameof(contractType));
        }

        return TryValidateValue(value, contractType, "args", new HashSet<Type>(), out errorMessage);
    }

    /// <summary> Validates that one deserialized public raw-op contract does not contain request-local alias references. </summary>
    /// <param name="value"> The contract object value. </param>
    /// <param name="contractType"> The contract type to inspect. </param>
    /// <param name="errorMessage"> The validation error message when validation fails. </param>
    /// <returns> <see langword="true" /> when the value does not contain request-local aliases; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="contractType" /> is <see langword="null" />. </exception>
    public static bool TryValidateNoRequestLocalAliases (
        object? value,
        Type contractType,
        out string errorMessage)
    {
        if (contractType == null)
        {
            throw new ArgumentNullException(nameof(contractType));
        }

        return UcliOperationRequestLocalAliasValueValidator.TryValidate(value, contractType, out errorMessage);
    }

    /// <summary> Validates that one public raw-op JSON args object does not contain request-local alias properties. </summary>
    /// <param name="value"> The raw JSON args value. </param>
    /// <param name="contractType"> The contract type to inspect. </param>
    /// <param name="errorMessage"> The validation error message when validation fails. </param>
    /// <returns> <see langword="true" /> when the JSON args object does not contain request-local alias properties; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="contractType" /> is <see langword="null" />. </exception>
    public static bool TryValidateNoRequestLocalAliasProperties (
        JsonElement value,
        Type contractType,
        out string errorMessage)
    {
        if (contractType == null)
        {
            throw new ArgumentNullException(nameof(contractType));
        }

        return UcliOperationRequestLocalAliasJsonValidator.TryValidate(value, contractType, out errorMessage);
    }

    /// <summary> Validates that one args contract type does not expose reserved public raw-op property names. </summary>
    /// <param name="contractType"> The contract type to inspect. </param>
    /// <param name="errorMessage"> The validation error message when validation fails. </param>
    /// <returns> <see langword="true" /> when the type can be exposed through public raw-op metadata; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="contractType" /> is <see langword="null" />. </exception>
    public static bool TryValidatePublicRawOpReservedProperties (
        Type contractType,
        out string errorMessage)
    {
        if (contractType == null)
        {
            throw new ArgumentNullException(nameof(contractType));
        }

        return UcliOperationPublicRawOpReservedPropertyValidator.TryValidate(contractType, out errorMessage);
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

        if (UcliOperationContractTypeFacts.IsScalar(actualType) || actualType == JsonElementType || actualType == typeof(object))
        {
            return TryValidateScalar(value!, path, out errorMessage);
        }

        if (UcliOperationContractTypeFacts.TryGetArrayElementType(actualType, out var elementType))
        {
            return TryValidateArray(value!, elementType!, path, visitedTypes, out errorMessage);
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
        if (value == null)
        {
            return true;
        }

        if (!visitedTypes.Add(contractType))
        {
            return true;
        }

        if (!TryValidateObjectProperties(value, contractType, path, visitedTypes, out errorMessage))
        {
            visitedTypes.Remove(contractType);
            return false;
        }

        if (!TryValidateObjectRules(value, contractType, path, out errorMessage))
        {
            visitedTypes.Remove(contractType);
            return false;
        }

        visitedTypes.Remove(contractType);
        return true;
    }

    private static bool TryValidateObjectProperties (
        object value,
        Type contractType,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        foreach (var property in UcliOperationContractReflection.GetContractProperties(contractType))
        {
            if (!TryValidateProperty(value, property, path, visitedTypes, out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateProperty (
        object owner,
        PropertyInfo property,
        string ownerPath,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        var propertyName = UcliOperationContractReflection.GetJsonPropertyName(property);
        var propertyValue = property.GetValue(owner);
        if (property.GetCustomAttribute<UcliRequiredAttribute>() != null
            && !UcliOperationContractTypeFacts.HasValue(propertyValue))
        {
            errorMessage = $"Operation '{ownerPath}' requires property '{propertyName}'.";
            return false;
        }

        return TryValidatePropertyValue(property, propertyValue, $"{ownerPath}.{propertyName}", visitedTypes, out errorMessage);
    }

    private static bool TryValidateObjectRules (
        object value,
        Type contractType,
        string path,
        out string errorMessage)
    {
        return TryValidateExclusiveRequiredPropertySets(value, contractType, path, out errorMessage)
            && TryValidatePropertyRequirements(value, contractType, path, out errorMessage);
    }

    private static bool TryValidatePropertyValue (
        PropertyInfo property,
        object? value,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (!UcliOperationContractTypeFacts.HasValue(value))
        {
            return true;
        }

        if (property.GetCustomAttribute<UcliJsonAnyValueAttribute>() != null)
        {
            return true;
        }

        var actualType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (!UcliOperationInputConstraintRuntimeValidator.TryValidate(property, value!, path, out errorMessage))
        {
            return false;
        }

        if (UcliOperationContractTypeFacts.TryGetArrayElementType(property.PropertyType, out var elementType))
        {
            return TryValidateArray(value!, elementType!, path, visitedTypes, out errorMessage);
        }

        if (UcliOperationContractTypeFacts.IsScalar(actualType) || actualType == JsonElementType || actualType == typeof(object))
        {
            return true;
        }

        return TryValidateObject(value, actualType, path, visitedTypes, out errorMessage);
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

    private static bool TryValidateScalar (
        object value,
        string path,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Undefined)
        {
            errorMessage = $"Operation '{path}' is required.";
            return false;
        }

        return true;
    }

    private static bool TryValidateExclusiveRequiredPropertySets (
        object value,
        Type contractType,
        string path,
        out string errorMessage)
    {
        var requiredPropertySets = contractType.GetCustomAttributes<UcliExclusiveRequiredPropertySetAttribute>().ToArray();
        if (requiredPropertySets.Length == 0)
        {
            errorMessage = string.Empty;
            return true;
        }

        var matchCount = 0;
        UcliExclusiveRequiredPropertySetAttribute? matchedRequiredPropertySet = null;
        for (var i = 0; i < requiredPropertySets.Length; i++)
        {
            if (IsRequiredPropertySetMatched(value, contractType, requiredPropertySets[i].RequiredPropertyNames))
            {
                matchCount++;
                matchedRequiredPropertySet = requiredPropertySets[i];
            }
        }

        if (matchCount == 1)
        {
            return TryValidateExclusiveRequiredPropertySetFields(
                value,
                contractType,
                matchedRequiredPropertySet!,
                requiredPropertySets,
                path,
                out errorMessage);
        }

        errorMessage = $"Operation '{path}' must match exactly one exclusive required property set.";
        return false;
    }

    private static bool TryValidateExclusiveRequiredPropertySetFields (
        object value,
        Type contractType,
        UcliExclusiveRequiredPropertySetAttribute matchedRequiredPropertySet,
        IReadOnlyList<UcliExclusiveRequiredPropertySetAttribute> requiredPropertySets,
        string path,
        out string errorMessage)
    {
        var allowedPropertyNames = new HashSet<string>(matchedRequiredPropertySet.RequiredPropertyNames, StringComparer.Ordinal);
        var exclusivePropertyNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < requiredPropertySets.Count; i++)
        {
            var propertyNames = requiredPropertySets[i].RequiredPropertyNames;
            for (var j = 0; j < propertyNames.Length; j++)
            {
                exclusivePropertyNames.Add(propertyNames[j]);
            }
        }

        foreach (var propertyName in exclusivePropertyNames)
        {
            if (!allowedPropertyNames.Contains(propertyName)
                && IsPropertyPresent(value, contractType, propertyName))
            {
                errorMessage = $"Operation '{path}' must not mix exclusive required property sets.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidatePropertyRequirements (
        object value,
        Type contractType,
        string path,
        out string errorMessage)
    {
        var requirements = contractType.GetCustomAttributes<UcliPropertyRequiresAttribute>().ToArray();
        for (var i = 0; i < requirements.Length; i++)
        {
            var rule = requirements[i];
            if (!IsPropertyPresent(value, contractType, rule.TriggerPropertyName))
            {
                continue;
            }

            if (!IsRequiredPropertySetMatched(value, contractType, rule.RequiredPropertyNames))
            {
                errorMessage = $"Operation '{path}' requires properties when '{rule.TriggerPropertyName}' is specified.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool IsRequiredPropertySetMatched (
        object value,
        Type contractType,
        IReadOnlyList<string> propertyNames)
    {
        for (var i = 0; i < propertyNames.Count; i++)
        {
            if (!IsPropertyPresent(value, contractType, propertyNames[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPropertyPresent (
        object value,
        Type contractType,
        string jsonPropertyName)
    {
        var properties = UcliOperationContractReflection.GetContractProperties(contractType);
        for (var i = 0; i < properties.Count; i++)
        {
            var property = properties[i];
            if (!string.Equals(UcliOperationContractReflection.GetJsonPropertyName(property), jsonPropertyName, StringComparison.Ordinal))
            {
                continue;
            }

            return UcliOperationContractTypeFacts.HasValue(property.GetValue(value));
        }

        return false;
    }
}
