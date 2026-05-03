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

        if (!TryValidateRequiredPropertyAlternatives(value, contractType, path, out errorMessage))
        {
            visitedTypes.Remove(contractType);
            return false;
        }

        if (!TryValidatePropertyDependencies(value, contractType, path, out errorMessage))
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

        if (property.GetCustomAttribute<UcliSchemaAnyAttribute>() != null)
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

    private static bool TryValidateRequiredPropertyAlternatives (
        object value,
        Type contractType,
        string path,
        out string errorMessage)
    {
        var alternatives = contractType.GetCustomAttributes<UcliRequiredPropertyAlternativeAttribute>().ToArray();
        if (alternatives.Length == 0)
        {
            errorMessage = string.Empty;
            return true;
        }

        var matchCount = 0;
        UcliRequiredPropertyAlternativeAttribute? matchedAlternative = null;
        for (var i = 0; i < alternatives.Length; i++)
        {
            if (IsAlternativeMatched(value, contractType, alternatives[i].RequiredPropertyNames))
            {
                matchCount++;
                matchedAlternative = alternatives[i];
            }
        }

        if (matchCount == 1)
        {
            return TryValidateAlternativePropertyExclusivity(
                value,
                contractType,
                matchedAlternative!,
                alternatives,
                path,
                out errorMessage);
        }

        errorMessage = $"Operation '{path}' must match exactly one required-property alternative.";
        return false;
    }

    private static bool TryValidateAlternativePropertyExclusivity (
        object value,
        Type contractType,
        UcliRequiredPropertyAlternativeAttribute matchedAlternative,
        IReadOnlyList<UcliRequiredPropertyAlternativeAttribute> alternatives,
        string path,
        out string errorMessage)
    {
        var allowedPropertyNames = new HashSet<string>(matchedAlternative.RequiredPropertyNames, StringComparer.Ordinal);
        var alternativePropertyNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < alternatives.Count; i++)
        {
            var propertyNames = alternatives[i].RequiredPropertyNames;
            for (var j = 0; j < propertyNames.Length; j++)
            {
                alternativePropertyNames.Add(propertyNames[j]);
            }
        }

        foreach (var propertyName in alternativePropertyNames)
        {
            if (!allowedPropertyNames.Contains(propertyName)
                && IsPropertyPresent(value, contractType, propertyName))
            {
                errorMessage = $"Operation '{path}' must not mix required-property alternatives.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidatePropertyDependencies (
        object value,
        Type contractType,
        string path,
        out string errorMessage)
    {
        var dependencies = contractType.GetCustomAttributes<UcliPropertyDependencyAttribute>().ToArray();
        for (var i = 0; i < dependencies.Length; i++)
        {
            var rule = dependencies[i];
            if (!IsPropertyPresent(value, contractType, rule.TriggerPropertyName))
            {
                continue;
            }

            if (!IsAlternativeMatched(value, contractType, rule.RequiredPropertyNames))
            {
                errorMessage = $"Operation '{path}' requires dependent properties when '{rule.TriggerPropertyName}' is specified.";
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

    private static bool IsAlternativeMatched (
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
