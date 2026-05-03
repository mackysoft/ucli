using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts operation side effects between enum and contract literals. </summary>
public static class UcliOperationSideEffectCodec
{
    private static readonly (UcliOperationSideEffect Value, string Literal)[] Mappings =
    {
        (UcliOperationSideEffect.OpensSceneInEditor, UcliOperationSideEffectValues.OpensSceneInEditor),
        (UcliOperationSideEffect.OpensPrefabStage, UcliOperationSideEffectValues.OpensPrefabStage),
        (UcliOperationSideEffect.RefreshesAssetDatabase, UcliOperationSideEffectValues.RefreshesAssetDatabase),
        (UcliOperationSideEffect.WritesAsset, UcliOperationSideEffectValues.WritesAsset),
        (UcliOperationSideEffect.WritesScene, UcliOperationSideEffectValues.WritesScene),
        (UcliOperationSideEffect.WritesPrefab, UcliOperationSideEffectValues.WritesPrefab),
        (UcliOperationSideEffect.WritesProjectSettings, UcliOperationSideEffectValues.WritesProjectSettings),
    };

    /// <summary> Converts one side-effect enum value to its contract literal. </summary>
    /// <param name="sideEffect"> The side-effect enum value. </param>
    /// <returns> The contract literal value. </returns>
    public static string ToValue (UcliOperationSideEffect sideEffect)
    {
        return LiteralCodecUtilities.ToValue(
            sideEffect,
            Mappings,
            nameof(sideEffect),
            "Unsupported operation side effect.");
    }
}
