using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Converts operation asset kinds between enum and contract literals. </summary>
public static class UcliOperationAssetKindCodec
{
    private static readonly (UcliOperationAssetKind Value, string Literal)[] Mappings =
    {
        (UcliOperationAssetKind.Asset, UcliOperationAssetKindValues.Asset),
        (UcliOperationAssetKind.Prefab, UcliOperationAssetKindValues.Prefab),
        (UcliOperationAssetKind.ProjectSettings, UcliOperationAssetKindValues.ProjectSettings),
        (UcliOperationAssetKind.Scene, UcliOperationAssetKindValues.Scene),
    };

    /// <summary> Converts one asset kind enum value to its contract literal. </summary>
    /// <param name="assetKind"> The asset kind enum value. </param>
    /// <returns> The contract literal value. </returns>
    public static string ToValue (UcliOperationAssetKind assetKind)
    {
        return LiteralCodecUtilities.ToValue(
            assetKind,
            Mappings,
            nameof(assetKind),
            "Unsupported operation asset kind.");
    }
}
