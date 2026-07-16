using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliOperationInputConstraintRuntimeValidator
{
    private static readonly HashSet<TypeCode> NumericTypeCodes = new()
    {
        TypeCode.Byte,
        TypeCode.SByte,
        TypeCode.Int16,
        TypeCode.UInt16,
        TypeCode.Int32,
        TypeCode.UInt32,
        TypeCode.Int64,
        TypeCode.UInt64,
        TypeCode.Single,
        TypeCode.Double,
        TypeCode.Decimal,
    };

    public static bool TryValidate (
        PropertyInfo property,
        object value,
        string path,
        out string errorMessage)
    {
        var attributes = UcliOperationContractReflection.GetInputConstraintAttributes(property);
        for (var i = 0; i < attributes.Length; i++)
        {
            if (!TryValidateAttribute(attributes[i], value, path, out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateAttribute (
        UcliInputConstraintAttribute attribute,
        object value,
        string path,
        out string errorMessage)
    {
        return attribute.Kind switch
        {
            UcliOperationInputConstraintKind.NonEmpty => TryValidateNonEmpty(value, path, out errorMessage),
            UcliOperationInputConstraintKind.Range => TryValidateRange(value, attribute, path, out errorMessage),
            UcliOperationInputConstraintKind.Cursor => TryValidateCursor(value, path, out errorMessage),
            _ => Succeed(out errorMessage),
        };
    }

    private static bool TryValidateNonEmpty (
        object value,
        string path,
        out string errorMessage)
    {
        if (IsEmpty(value))
        {
            errorMessage = $"Operation '{path}' must not be empty.";
            return false;
        }

        return Succeed(out errorMessage);
    }

    private static bool TryValidateRange (
        object value,
        UcliInputConstraintAttribute attribute,
        string path,
        out string errorMessage)
    {
        if (!TryConvertToDouble(value, out var number))
        {
            return Succeed(out errorMessage);
        }

        return TryValidateRangeBounds(number, attribute, path, out errorMessage);
    }

    private static bool TryValidateCursor (
        object value,
        string path,
        out string errorMessage)
    {
        if (!TryConvertToCursorText(value, out var cursor)
            || BoundedWindowCursorCodec.TryDecode(cursor, out _))
        {
            return Succeed(out errorMessage);
        }

        errorMessage = $"Operation '{path}' must be a valid cursor.";
        return false;
    }

    private static bool TryValidateRangeBounds (
        double number,
        UcliInputConstraintAttribute attribute,
        string path,
        out string errorMessage)
    {
        if (attribute.HasMin && number < attribute.Min)
        {
            errorMessage = $"Operation '{path}' must be greater than or equal to {attribute.Min.ToString(CultureInfo.InvariantCulture)}.";
            return false;
        }

        if (attribute.HasMax && number > attribute.Max)
        {
            errorMessage = $"Operation '{path}' must be less than or equal to {attribute.Max.ToString(CultureInfo.InvariantCulture)}.";
            return false;
        }

        return Succeed(out errorMessage);
    }

    private static bool IsEmpty (object value)
    {
        return value switch
        {
            string text => string.IsNullOrWhiteSpace(text),
            JsonElement jsonElement => IsEmptyJsonElement(jsonElement),
            IEnumerable enumerable => !HasAny(enumerable),
            _ => false,
        };
    }

    private static bool IsEmptyJsonElement (JsonElement jsonElement)
    {
        return jsonElement.ValueKind switch
        {
            JsonValueKind.Object => !HasAnyJsonObjectProperty(jsonElement),
            JsonValueKind.Array => !HasAnyJsonArrayItem(jsonElement),
            _ => false,
        };
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
        var typeCode = Type.GetTypeCode(value.GetType());
        if (value is IConvertible convertible && NumericTypeCodes.Contains(typeCode))
        {
            number = convertible.ToDouble(CultureInfo.InvariantCulture);
            return true;
        }

        number = 0;
        return false;
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

    private static bool Succeed (out string errorMessage)
    {
        errorMessage = string.Empty;
        return true;
    }
}
