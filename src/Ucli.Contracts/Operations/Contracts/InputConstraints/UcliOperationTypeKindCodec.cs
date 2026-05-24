using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Converts operation type kinds between enum and contract literals. </summary>
public static class UcliOperationTypeKindCodec
{
    private static readonly (UcliOperationTypeKind Value, string Literal)[] Mappings =
    {
        (UcliOperationTypeKind.Component, UcliOperationTypeKindValues.Component),
    };

    /// <summary> Converts one type kind enum value to its contract literal. </summary>
    /// <param name="typeKind"> The type kind enum value. </param>
    /// <returns> The contract literal value. </returns>
    public static string ToValue (UcliOperationTypeKind typeKind)
    {
        return LiteralCodecUtilities.ToValue(
            typeKind,
            Mappings,
            nameof(typeKind),
            "Unsupported operation type kind.");
    }
}
