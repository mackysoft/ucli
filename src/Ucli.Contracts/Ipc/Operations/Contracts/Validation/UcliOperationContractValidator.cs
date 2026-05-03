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

        if (TryGetArrayElementType(property.PropertyType, out var elementType))
        {
            return TryValidateArray(value!, elementType!, path, visitedTypes, out errorMessage);
        }

        var actualType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
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
        for (var i = 0; i < alternatives.Length; i++)
        {
            if (IsAlternativeMatched(value, contractType, alternatives[i].RequiredPropertyNames))
            {
                matchCount++;
            }
        }

        if (matchCount == 1)
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = $"Operation '{path}' must match exactly one required-property alternative.";
        return false;
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
