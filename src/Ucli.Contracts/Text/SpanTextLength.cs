namespace MackySoft.Ucli.Contracts.Text;

/// <summary> Calculates fixed-size span text lengths before formatting. </summary>
internal static class SpanTextLength
{
    /// <summary> Returns the invariant-culture decimal text length for one integer value. </summary>
    public static int GetInvariantInt64Length (long value)
    {
        if (value == 0)
        {
            return 1;
        }

        var length = value < 0 ? 1 : 0;
        var remaining = value;
        while (remaining != 0)
        {
            length++;
            remaining /= 10;
        }

        return length;
    }

    /// <summary> Returns the prefixed length for one optional string value. </summary>
    public static int GetOptionalStringLength (
        string prefix,
        string? value)
    {
        return value is null ? 0 : checked(prefix.Length + value.Length);
    }

    /// <summary> Returns the prefixed length for one optional integer value. </summary>
    public static int GetOptionalInvariantInt64Length (
        string prefix,
        long? value)
    {
        return value.HasValue ? checked(prefix.Length + GetInvariantInt64Length(value.Value)) : 0;
    }

    /// <summary> Returns the lowercase boolean text length. </summary>
    public static int GetBoolLength (bool value)
    {
        return value ? 4 : 5;
    }

    /// <summary> Returns the prefixed length for one optional boolean value. </summary>
    public static int GetOptionalBoolLength (
        string prefix,
        bool? value)
    {
        return value.HasValue ? checked(prefix.Length + GetBoolLength(value.Value)) : 0;
    }
}
