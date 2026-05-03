using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Validates deserialized operation contract objects against structural uCLI contract attributes. </summary>
public static class UcliOperationContractValidator
{
    private static readonly Type StringType = typeof(string);

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

    private static bool TryValidateValue (
        object? value,
        Type contractType,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        var actualType = Nullable.GetUnderlyingType(contractType) ?? contractType;
        if (!HasValue(value))
        {
            return true;
        }

        if (IsScalar(actualType) || actualType == JsonElementType || actualType == typeof(object))
        {
            return TryValidateScalar(value!, path, out errorMessage);
        }

        if (TryGetArrayElementType(actualType, out var elementType))
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

        var properties = UcliOperationContractReflection.GetSchemaProperties(contractType);
        foreach (var property in properties)
        {
            var propertyName = UcliOperationContractReflection.GetJsonPropertyName(property);
            var propertyValue = property.GetValue(value);
            var hasValue = HasValue(propertyValue);

            if (property.GetCustomAttribute<UcliRequiredAttribute>() != null && !hasValue)
            {
                errorMessage = $"Operation '{path}' requires property '{propertyName}'.";
                visitedTypes.Remove(contractType);
                return false;
            }

            if (!TryValidatePropertyValue(property, propertyValue, $"{path}.{propertyName}", visitedTypes, out errorMessage))
            {
                visitedTypes.Remove(contractType);
                return false;
            }
        }

        if (!TryValidateExclusiveRequiredPropertySets(value, contractType, path, out errorMessage))
        {
            visitedTypes.Remove(contractType);
            return false;
        }

        if (!TryValidatePropertyRequirements(value, contractType, path, out errorMessage))
        {
            visitedTypes.Remove(contractType);
            return false;
        }

        visitedTypes.Remove(contractType);
        return true;
    }

    private static bool TryValidatePropertyValue (
        PropertyInfo property,
        object? value,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (!HasValue(value))
        {
            return true;
        }

        if (property.GetCustomAttribute<UcliJsonAnyValueAttribute>() != null)
        {
            return true;
        }

        var actualType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (!TryValidateSupportedInputConstraints(property, value!, actualType, path, out errorMessage))
        {
            return false;
        }

        if (TryGetArrayElementType(property.PropertyType, out var elementType))
        {
            return TryValidateArray(value!, elementType!, path, visitedTypes, out errorMessage);
        }

        if (IsScalar(actualType) || actualType == JsonElementType || actualType == typeof(object))
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

    private static bool TryValidateSupportedInputConstraints (
        PropertyInfo property,
        object value,
        Type actualType,
        string path,
        out string errorMessage)
    {
        var attributes = UcliOperationContractReflection.GetInputConstraintAttributes(property);
        for (var i = 0; i < attributes.Length; i++)
        {
            var attribute = attributes[i];
            switch (attribute.Kind)
            {
                case UcliOperationInputConstraintKind.NonEmpty:
                    if (!TryValidateNonEmpty(value, path, out errorMessage))
                    {
                        return false;
                    }

                    break;

                case UcliOperationInputConstraintKind.Range:
                    if (!TryValidateRange(value, attribute, path, out errorMessage))
                    {
                        return false;
                    }

                    break;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateNonEmpty (
        object value,
        string path,
        out string errorMessage)
    {
        switch (value)
        {
            case string text when string.IsNullOrWhiteSpace(text):
                errorMessage = $"Operation '{path}' must not be empty.";
                return false;

            case UcliStringValue semanticString when string.IsNullOrWhiteSpace(semanticString.Value):
                errorMessage = $"Operation '{path}' must not be empty.";
                return false;

            case JsonElement jsonElement when IsEmptyJsonElement(jsonElement):
                errorMessage = $"Operation '{path}' must not be empty.";
                return false;

            case IEnumerable enumerable when !HasAny(enumerable):
                errorMessage = $"Operation '{path}' must not be empty.";
                return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateRange (
        object value,
        UcliInputConstraintAttribute attribute,
        string path,
        out string errorMessage)
    {
        if (!TryConvertToDouble(value, out var number))
        {
            errorMessage = string.Empty;
            return true;
        }

        if (!double.IsNaN(attribute.Min) && number < attribute.Min)
        {
            errorMessage = $"Operation '{path}' must be greater than or equal to {attribute.Min.ToString(CultureInfo.InvariantCulture)}.";
            return false;
        }

        if (!double.IsNaN(attribute.Max) && number > attribute.Max)
        {
            errorMessage = $"Operation '{path}' must be less than or equal to {attribute.Max.ToString(CultureInfo.InvariantCulture)}.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool IsEmptyJsonElement (JsonElement jsonElement)
    {
        switch (jsonElement.ValueKind)
        {
            case JsonValueKind.Object:
                return !HasAnyJsonObjectProperty(jsonElement);

            case JsonValueKind.Array:
                return !HasAnyJsonArrayItem(jsonElement);

            default:
                return false;
        }
    }

    private static bool HasAnyJsonObjectProperty (JsonElement jsonElement)
    {
        using var enumerator = jsonElement.EnumerateObject();
        return enumerator.MoveNext();
    }

    private static bool HasAnyJsonArrayItem (JsonElement jsonElement)
    {
        using var enumerator = jsonElement.EnumerateArray();
        return enumerator.MoveNext();
    }

    private static bool TryConvertToDouble (
        object value,
        out double number)
    {
        switch (value)
        {
            case byte typed:
                number = typed;
                return true;

            case sbyte typed:
                number = typed;
                return true;

            case short typed:
                number = typed;
                return true;

            case ushort typed:
                number = typed;
                return true;

            case int typed:
                number = typed;
                return true;

            case uint typed:
                number = typed;
                return true;

            case long typed:
                number = typed;
                return true;

            case ulong typed:
                number = typed;
                return true;

            case float typed:
                number = typed;
                return true;

            case double typed:
                number = typed;
                return true;

            case decimal typed:
                number = (double)typed;
                return true;

            default:
                number = 0;
                return false;
        }
    }

    private static bool HasAny (IEnumerable enumerable)
    {
        var enumerator = enumerable.GetEnumerator();
        try
        {
            return enumerator.MoveNext();
        }
        finally
        {
            if (enumerator is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
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
        var properties = UcliOperationContractReflection.GetSchemaProperties(contractType);
        for (var i = 0; i < properties.Count; i++)
        {
            var property = properties[i];
            if (!string.Equals(UcliOperationContractReflection.GetJsonPropertyName(property), jsonPropertyName, StringComparison.Ordinal))
            {
                continue;
            }

            return HasValue(property.GetValue(value));
        }

        return false;
    }

    private static bool HasValue (object? value)
    {
        return value switch
        {
            null => false,
            JsonElement jsonElement => jsonElement.ValueKind != JsonValueKind.Undefined,
            _ => true,
        };
    }

    private static bool TryGetArrayElementType (
        Type type,
        out Type? elementType)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        if (actualType.IsArray)
        {
            elementType = actualType.GetElementType();
            return elementType != null;
        }

        if (actualType.IsGenericType)
        {
            var genericTypeDefinition = actualType.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(IReadOnlyList<>)
                || genericTypeDefinition == typeof(IReadOnlyCollection<>)
                || genericTypeDefinition == typeof(IEnumerable<>)
                || genericTypeDefinition == typeof(List<>))
            {
                elementType = actualType.GetGenericArguments()[0];
                return true;
            }
        }

        elementType = null;
        return false;
    }

    private static bool IsScalar (Type type)
    {
        return type == StringType
            || UcliStringValue.IsAssignableFrom(type)
            || type == typeof(bool)
            || type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal);
    }
}
