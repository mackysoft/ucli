using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts operation reference target kinds between enum and contract literals. </summary>
public static class UcliOperationReferenceTargetKindCodec
{
    private static readonly (UcliOperationReferenceTargetKind Value, string Literal)[] Mappings =
    {
        (UcliOperationReferenceTargetKind.Asset, UcliOperationReferenceTargetKindValues.Asset),
        (UcliOperationReferenceTargetKind.Component, UcliOperationReferenceTargetKindValues.Component),
        (UcliOperationReferenceTargetKind.GameObject, UcliOperationReferenceTargetKindValues.GameObject),
    };

    /// <summary> Converts one reference target kind enum value to its contract literal. </summary>
    /// <param name="targetKind"> The reference target kind enum value. </param>
    /// <returns> The contract literal value. </returns>
    public static string ToValue (UcliOperationReferenceTargetKind targetKind)
    {
        return LiteralCodecUtilities.ToValue(
            targetKind,
            Mappings,
            nameof(targetKind),
            "Unsupported operation reference target kind.");
    }
}
