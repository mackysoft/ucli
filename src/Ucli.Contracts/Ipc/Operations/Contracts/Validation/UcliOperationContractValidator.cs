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

        return TryValidateNoRequestLocalAliasesValue(value, contractType, "args", new HashSet<Type>(), out errorMessage);
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

        return TryValidateNoRequestLocalAliasPropertiesValue(value, contractType, "args", new HashSet<Type>(), out errorMessage);
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

        return TryValidatePublicRawOpReservedProperties(contractType, "args", new HashSet<Type>(), out errorMessage);
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

    private static bool TryValidateNoRequestLocalAliasesValue (
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

        if (UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasValueType(actualType) || IsRequestLocalAliasValue(value))
        {
            errorMessage = $"Operation '{path}' cannot use request-local alias references in public op steps.";
            return false;
        }

        if (TryGetArrayElementType(actualType, out var elementType))
        {
            return TryValidateNoRequestLocalAliasesArray(value!, elementType!, path, visitedTypes, out errorMessage);
        }

        if (IsScalar(actualType) || actualType == JsonElementType || actualType == typeof(object))
        {
            return true;
        }

        return TryValidateNoRequestLocalAliasesObject(value, actualType, path, visitedTypes, out errorMessage);
    }

    private static bool TryValidateNoRequestLocalAliasesObject (
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

        var properties = UcliOperationContractReflection.GetContractProperties(contractType);
        foreach (var property in properties)
        {
            var propertyValue = property.GetValue(value);
            var propertyName = UcliOperationContractReflection.GetJsonPropertyName(property);
            if (UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasPropertyName(propertyName))
            {
                if (HasValue(propertyValue))
                {
                    errorMessage = $"Operation '{path}.{propertyName}' cannot use reserved request-local alias property '{UcliOperationContractPropertyNames.Alias}' in public op steps.";
                    visitedTypes.Remove(contractType);
                    return false;
                }

                continue;
            }

            if (!HasValue(propertyValue) || property.GetCustomAttribute<UcliJsonAnyValueAttribute>() != null)
            {
                continue;
            }

            if (!TryValidateNoRequestLocalAliasesValue(
                    propertyValue,
                    property.PropertyType,
                    $"{path}.{propertyName}",
                    visitedTypes,
                    out errorMessage))
            {
                visitedTypes.Remove(contractType);
                return false;
            }
        }

        visitedTypes.Remove(contractType);
        return true;
    }

    private static bool TryValidateNoRequestLocalAliasesArray (
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
            if (!TryValidateNoRequestLocalAliasesValue(item, elementType, $"{path}[{index.ToString(CultureInfo.InvariantCulture)}]", visitedTypes, out errorMessage))
            {
                return false;
            }

            index++;
        }

        return true;
    }

    private static bool TryValidateNoRequestLocalAliasPropertiesValue (
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

        if (TryGetArrayElementType(actualType, out var elementType))
        {
            return TryValidateNoRequestLocalAliasPropertiesArray(value, elementType!, path, visitedTypes, out errorMessage);
        }

        if (IsScalar(actualType) || actualType == JsonElementType || actualType == typeof(object))
        {
            return true;
        }

        return TryValidateNoRequestLocalAliasPropertiesObject(value, actualType, path, visitedTypes, out errorMessage);
    }

    private static bool TryValidateNoRequestLocalAliasPropertiesObject (
        JsonElement value,
        Type contractType,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (value.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        if (!visitedTypes.Add(contractType))
        {
            return true;
        }

        var properties = UcliOperationContractReflection.GetContractProperties(contractType);
        foreach (var property in properties)
        {
            var propertyName = UcliOperationContractReflection.GetJsonPropertyName(property);
            if (!value.TryGetProperty(propertyName, out var propertyValue))
            {
                continue;
            }

            if (UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasPropertyName(propertyName))
            {
                errorMessage = $"Operation '{path}.{propertyName}' cannot use reserved request-local alias property '{UcliOperationContractPropertyNames.Alias}' in public op steps.";
                visitedTypes.Remove(contractType);
                return false;
            }

            if (property.GetCustomAttribute<UcliJsonAnyValueAttribute>() != null)
            {
                continue;
            }

            if (!TryValidateNoRequestLocalAliasPropertiesValue(
                propertyValue,
                property.PropertyType,
                $"{path}.{propertyName}",
                visitedTypes,
                out errorMessage))
            {
                visitedTypes.Remove(contractType);
                return false;
            }
        }

        visitedTypes.Remove(contractType);
        return true;
    }

    private static bool TryValidateNoRequestLocalAliasPropertiesArray (
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
            if (!TryValidateNoRequestLocalAliasPropertiesValue(item, elementType, $"{path}[{index.ToString(CultureInfo.InvariantCulture)}]", visitedTypes, out errorMessage))
            {
                return false;
            }

            index++;
        }

        return true;
    }

    private static bool TryValidatePublicRawOpReservedProperties (
        Type contractType,
        string path,
        HashSet<Type> visitedTypes,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        var actualType = Nullable.GetUnderlyingType(contractType) ?? contractType;
        if (UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasValueType(actualType))
        {
            errorMessage = $"Operation contract property '{path}' uses internal request-local alias type '{actualType.Name}'.";
            return false;
        }

        if (IsScalar(actualType) || actualType == JsonElementType || actualType == typeof(object))
        {
            return true;
        }

        if (TryGetArrayElementType(actualType, out var elementType))
        {
            return TryValidatePublicRawOpReservedProperties(elementType!, path + "[]", visitedTypes, out errorMessage);
        }

        if (!visitedTypes.Add(actualType))
        {
            return true;
        }

        var properties = UcliOperationContractReflection.GetContractProperties(actualType);
        foreach (var property in properties)
        {
            var propertyName = UcliOperationContractReflection.GetJsonPropertyName(property);
            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (UcliRequestLocalAliasContractPolicy.IsInternalRequestLocalAliasBranchProperty(property))
            {
                continue;
            }

            if (UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasValueType(propertyType))
            {
                errorMessage = $"Operation contract property '{path}.{propertyName}' uses internal request-local alias type '{propertyType.Name}'.";
                visitedTypes.Remove(actualType);
                return false;
            }

            if (UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasPropertyName(propertyName))
            {
                errorMessage = $"Operation contract property '{path}.{propertyName}' uses reserved public raw-op property name '{UcliOperationContractPropertyNames.Alias}'.";
                visitedTypes.Remove(actualType);
                return false;
            }

            if (property.GetCustomAttribute<UcliJsonAnyValueAttribute>() != null)
            {
                continue;
            }

            if (!TryValidatePublicRawOpReservedProperties(property.PropertyType, $"{path}.{propertyName}", visitedTypes, out errorMessage))
            {
                visitedTypes.Remove(actualType);
                return false;
            }
        }

        visitedTypes.Remove(actualType);
        return true;
    }

    private static bool IsRequestLocalAliasValue (object? value)
    {
        return value != null && UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasValueType(value.GetType());
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

        var properties = UcliOperationContractReflection.GetContractProperties(contractType);
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

                case UcliOperationInputConstraintKind.Cursor:
                    if (!TryValidateCursor(value, path, out errorMessage))
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

    private static bool TryValidateCursor (
        object value,
        string path,
        out string errorMessage)
    {
        if (!TryConvertToCursorText(value, out var cursor))
        {
            errorMessage = string.Empty;
            return true;
        }

        if (!BoundedWindowCursorCodec.TryDecode(cursor, out _))
        {
            errorMessage = $"Operation '{path}' must be a valid cursor.";
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

    private static bool TryConvertToCursorText (
        object value,
        out string? cursor)
    {
        switch (value)
        {
            case string text:
                cursor = text;
                return true;

            case UcliStringValue semanticString:
                cursor = semanticString.Value;
                return true;

            case JsonElement { ValueKind: JsonValueKind.String } jsonElement:
                cursor = jsonElement.GetString();
                return true;

            default:
                cursor = null;
                return false;
        }
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
        var properties = UcliOperationContractReflection.GetContractProperties(contractType);
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
            || type.IsEnum
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
