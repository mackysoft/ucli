using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliOperationContractTypeFacts
{
    private static readonly Type StringType = typeof(string);

    private static readonly Type JsonElementType = typeof(JsonElement);

    public static bool HasValue (object? value)
    {
        return value switch
        {
            null => false,
            JsonElement jsonElement => jsonElement.ValueKind != JsonValueKind.Undefined,
            _ => true,
        };
    }

    public static bool TryGetArrayElementType (
        Type type,
        out Type? elementType)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        if (actualType.IsArray)
        {
            elementType = actualType.GetElementType();
            return elementType != null;
        }

        if (actualType.IsGenericType && IsSupportedGenericArray(actualType))
        {
            elementType = actualType.GetGenericArguments()[0];
            return true;
        }

        elementType = null;
        return false;
    }

    public static bool IsScalar (Type type)
    {
        return type == StringType
            || type.IsEnum
            || UcliStringValue.IsAssignableFrom(type)
            || type == typeof(bool)
            || IsInteger(type)
            || IsNumber(type);
    }

    private static bool IsSupportedGenericArray (Type type)
    {
        var genericTypeDefinition = type.GetGenericTypeDefinition();
        return genericTypeDefinition == typeof(IReadOnlyList<>)
            || genericTypeDefinition == typeof(IReadOnlyCollection<>)
            || genericTypeDefinition == typeof(IEnumerable<>)
            || genericTypeDefinition == typeof(List<>);
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
