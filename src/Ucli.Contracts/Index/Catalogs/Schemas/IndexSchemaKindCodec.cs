namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Converts index schema-kind values between enum and contract literals. </summary>
public static class IndexSchemaKindCodec
{
    /// <summary> Converts one index schema-kind enum value to literal. </summary>
    /// <param name="kind"> The schema-kind enum value. </param>
    /// <returns> The schema-kind literal. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="kind" /> is unsupported. </exception>
    public static string ToValue (IndexSchemaKind kind)
    {
        return kind switch
        {
            IndexSchemaKind.Comp => IndexSchemaKindValues.Comp,
            IndexSchemaKind.Asset => IndexSchemaKindValues.Asset,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported index schema kind."),
        };
    }

    /// <summary> Tries to parse one schema-kind literal to enum value. </summary>
    /// <param name="value"> The schema-kind literal. </param>
    /// <param name="kind"> The parsed enum value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out IndexSchemaKind kind)
    {
        if (string.Equals(value, IndexSchemaKindValues.Comp, StringComparison.OrdinalIgnoreCase))
        {
            kind = IndexSchemaKind.Comp;
            return true;
        }

        if (string.Equals(value, IndexSchemaKindValues.Asset, StringComparison.OrdinalIgnoreCase))
        {
            kind = IndexSchemaKind.Asset;
            return true;
        }

        kind = default;
        return false;
    }
}
