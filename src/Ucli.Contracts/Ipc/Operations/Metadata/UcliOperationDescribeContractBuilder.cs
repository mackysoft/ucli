using System.Reflection;
using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Builds operation describe contracts from typed args and result contract metadata. </summary>
public static class UcliOperationDescribeContractBuilder
{
    private static readonly Type StringType = typeof(string);

    private static readonly Type JsonElementType = typeof(JsonElement);

    /// <summary> Creates an operation describe contract from typed args and result contract types. </summary>
    /// <typeparam name="TArgs"> The operation args contract type. </typeparam>
    /// <typeparam name="TResult"> The operation result contract type. </typeparam>
    /// <param name="description"> The operation purpose description. </param>
    /// <param name="assurance"> The operation assurance metadata. </param>
    /// <returns> The operation describe contract. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="description" /> is empty. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="assurance" /> is <see langword="null" />. </exception>
    public static UcliOperationDescribeContract Create<TArgs, TResult> (
        string description,
        UcliOperationAssuranceContract assurance)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Operation description must not be null, empty, or whitespace.", nameof(description));
        }

        if (assurance == null)
        {
            throw new ArgumentNullException(nameof(assurance));
        }

        return new UcliOperationDescribeContract(
            description,
            CreateInputs(typeof(TArgs)),
            CreateResultContract(typeof(TResult)),
            assurance);
    }

    private static IReadOnlyList<UcliOperationInputContract> CreateInputs (Type argsType)
    {
        var properties = UcliOperationContractReflection.GetSchemaProperties(argsType);
        if (properties.Count == 0)
        {
            return Array.Empty<UcliOperationInputContract>();
        }

        var inputs = new UcliOperationInputContract[properties.Count];
        for (var i = 0; i < properties.Count; i++)
        {
            inputs[i] = CreateInput(properties[i]);
        }

        return inputs;
    }

    private static UcliOperationInputContract CreateInput (PropertyInfo property)
    {
        var name = UcliOperationContractReflection.GetJsonPropertyName(property);
        var description = GetDescription(property);
        var valueType = GetValueType(property.PropertyType);
        var constraints = CreateConstraints(property);
        var variants = CreateVariants(property, "$." + name);

        return new UcliOperationInputContract(
            name,
            valueType,
            description,
            constraints,
            argsPath: null,
            variants: variants.Count == 0 ? null : variants);
    }

    private static IReadOnlyList<UcliOperationInputVariantContract> CreateVariants (
        PropertyInfo property,
        string prefix)
    {
        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var alternatives = propertyType.GetCustomAttributes<UcliRequiredPropertyAlternativeAttribute>().ToArray();
        if (alternatives.Length == 0)
        {
            return Array.Empty<UcliOperationInputVariantContract>();
        }

        var variants = new UcliOperationInputVariantContract[alternatives.Length];
        for (var i = 0; i < alternatives.Length; i++)
        {
            variants[i] = CreateVariant(propertyType, alternatives[i], prefix);
        }

        return variants;
    }

    private static UcliOperationInputVariantContract CreateVariant (
        Type contractType,
        UcliRequiredPropertyAlternativeAttribute alternative,
        string prefix)
    {
        var fields = ResolveAlternativeFields(contractType, alternative.RequiredPropertyNames);
        var argsPaths = new string[fields.Length];
        var constraints = new List<UcliOperationInputConstraintContract>();
        for (var i = 0; i < fields.Length; i++)
        {
            var fieldName = UcliOperationContractReflection.GetJsonPropertyName(fields[i]);
            argsPaths[i] = prefix + "." + fieldName;
            constraints.AddRange(CreateConstraints(fields[i]));
        }

        return new UcliOperationInputVariantContract(
            CreateVariantName(fields),
            CreateVariantDescription(fields),
            argsPaths,
            constraints);
    }

    private static PropertyInfo[] ResolveAlternativeFields (
        Type contractType,
        IReadOnlyList<string> requiredPropertyNames)
    {
        var properties = UcliOperationContractReflection.GetSchemaProperties(contractType);
        var fields = new PropertyInfo[requiredPropertyNames.Count];
        for (var i = 0; i < requiredPropertyNames.Count; i++)
        {
            var requiredPropertyName = requiredPropertyNames[i];
            fields[i] = properties.FirstOrDefault(property =>
                    string.Equals(UcliOperationContractReflection.GetJsonPropertyName(property), requiredPropertyName, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"Required-property alternative references unknown property '{requiredPropertyName}' on '{contractType.FullName}'.");
        }

        return fields;
    }

    private static string CreateVariantName (IReadOnlyList<PropertyInfo> fields)
    {
        var parts = new string[fields.Count];
        for (var i = 0; i < fields.Count; i++)
        {
            parts[i] = fields[i].Name;
        }

        return "by" + string.Concat(parts);
    }

    private static string CreateVariantDescription (IReadOnlyList<PropertyInfo> fields)
    {
        if (fields.Count == 1)
        {
            return "Use " + GetDescription(fields[0]);
        }

        var descriptions = new string[fields.Count];
        for (var i = 0; i < fields.Count; i++)
        {
            descriptions[i] = GetDescription(fields[i]);
        }

        return "Use " + string.Join(" and ", descriptions);
    }

    private static IReadOnlyList<UcliOperationInputConstraintContract> CreateConstraints (PropertyInfo property)
    {
        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var typeAttributes = propertyType.GetCustomAttributes<UcliInputConstraintAttribute>().ToArray();
        var propertyAttributes = property.GetCustomAttributes<UcliInputConstraintAttribute>().ToArray();
        var attributes = new UcliInputConstraintAttribute[typeAttributes.Length + propertyAttributes.Length];
        Array.Copy(typeAttributes, attributes, typeAttributes.Length);
        Array.Copy(propertyAttributes, 0, attributes, typeAttributes.Length, propertyAttributes.Length);

        if (attributes.Length == 0)
        {
            return Array.Empty<UcliOperationInputConstraintContract>();
        }

        var constraints = new UcliOperationInputConstraintContract[attributes.Length];
        for (var i = 0; i < attributes.Length; i++)
        {
            constraints[i] = attributes[i].ToContract();
        }

        return constraints;
    }

    private static UcliOperationResultContract CreateResultContract (Type resultType)
    {
        if (resultType == typeof(UcliNoResult))
        {
            return UcliOperationResultContract.NoResult("This operation does not emit operation-specific result data.");
        }

        return new UcliOperationResultContract(
            emitted: true,
            resultType: resultType.Name,
            description: GetDescription(resultType));
    }

    private static string GetDescription (MemberInfo member)
    {
        if (member is PropertyInfo property)
        {
            var description = property.GetCustomAttribute<UcliDescriptionAttribute>()?.Description;
            if (description != null)
            {
                return description;
            }

            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (UcliStringValue.IsAssignableFrom(propertyType))
            {
                return GetDescription(propertyType);
            }
        }

        return member.GetCustomAttribute<UcliDescriptionAttribute>()?.Description
            ?? throw new InvalidOperationException($"Operation contract member '{member.Name}' must declare {nameof(UcliDescriptionAttribute)}.");
    }

    private static string GetValueType (Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        if (actualType == StringType || UcliStringValue.IsAssignableFrom(actualType))
        {
            return "string";
        }

        if (actualType == typeof(bool))
        {
            return "boolean";
        }

        if (IsInteger(actualType))
        {
            return "integer";
        }

        if (IsNumber(actualType))
        {
            return "number";
        }

        if (actualType == JsonElementType || actualType == typeof(object))
        {
            return "object";
        }

        return TryGetArrayElementType(actualType, out _) ? "array" : "object";
    }

    private static bool TryGetArrayElementType (
        Type type,
        out Type? elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType();
            return elementType != null;
        }

        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(IReadOnlyList<>)
                || genericTypeDefinition == typeof(IReadOnlyCollection<>)
                || genericTypeDefinition == typeof(IEnumerable<>)
                || genericTypeDefinition == typeof(List<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }
        }

        elementType = null;
        return false;
    }

    private static bool IsInteger (Type type)
    {
        return type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong);
    }

    private static bool IsNumber (Type type)
    {
        return type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal);
    }
}
