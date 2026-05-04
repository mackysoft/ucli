using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Converts operation-kind values between enum and contract literals. </summary>
public static class UcliOperationKindCodec
{
    private static readonly (UcliOperationKind Value, string Literal)[] Mappings =
    {
        (UcliOperationKind.Query, UcliOperationKindValues.Query),
        (UcliOperationKind.Mutation, UcliOperationKindValues.Mutation),
        (UcliOperationKind.Command, UcliOperationKindValues.Command),
    };

    /// <summary> Converts one operation-kind enum value to contract literal. </summary>
    /// <param name="operationKind"> The operation-kind enum value. </param>
    /// <returns> The contract literal value. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="operationKind" /> is unsupported. </exception>
    public static string ToValue (UcliOperationKind operationKind)
    {
        return LiteralCodecUtilities.ToValue(
            operationKind,
            Mappings,
            nameof(operationKind),
            "Unsupported operationKind.");
    }

    /// <summary> Tries to parse contract literal to operation-kind enum. </summary>
    /// <param name="value"> The contract literal value. </param>
    /// <param name="operationKind"> The parsed enum value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out UcliOperationKind operationKind)
    {
        return LiteralCodecUtilities.TryParse(
            value,
            Mappings,
            StringComparison.OrdinalIgnoreCase,
            out operationKind);
    }
}
