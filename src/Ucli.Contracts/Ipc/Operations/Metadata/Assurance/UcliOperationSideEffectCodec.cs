using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts operation side effects between enum and contract literals. </summary>
public static class UcliOperationSideEffectCodec
{
    private static readonly (UcliOperationSideEffect Value, string Literal)[] Mappings =
    {
        (UcliOperationSideEffect.ObservesUnityState, UcliOperationSideEffectValues.ObservesUnityState),
        (UcliOperationSideEffect.EditorStateChange, UcliOperationSideEffectValues.EditorStateChange),
        (UcliOperationSideEffect.OpensSceneInEditor, UcliOperationSideEffectValues.OpensSceneInEditor),
        (UcliOperationSideEffect.OpensPrefabStage, UcliOperationSideEffectValues.OpensPrefabStage),
        (UcliOperationSideEffect.AssetDatabaseRefresh, UcliOperationSideEffectValues.AssetDatabaseRefresh),
        (UcliOperationSideEffect.AssetImport, UcliOperationSideEffectValues.AssetImport),
        (UcliOperationSideEffect.ScriptCompilation, UcliOperationSideEffectValues.ScriptCompilation),
        (UcliOperationSideEffect.DomainReload, UcliOperationSideEffectValues.DomainReload),
        (UcliOperationSideEffect.SceneContentMutation, UcliOperationSideEffectValues.SceneContentMutation),
        (UcliOperationSideEffect.PrefabContentMutation, UcliOperationSideEffectValues.PrefabContentMutation),
        (UcliOperationSideEffect.AssetContentMutation, UcliOperationSideEffectValues.AssetContentMutation),
        (UcliOperationSideEffect.ProjectSettingsMutation, UcliOperationSideEffectValues.ProjectSettingsMutation),
        (UcliOperationSideEffect.SceneSave, UcliOperationSideEffectValues.SceneSave),
        (UcliOperationSideEffect.PrefabSave, UcliOperationSideEffectValues.PrefabSave),
        (UcliOperationSideEffect.AssetSave, UcliOperationSideEffectValues.AssetSave),
        (UcliOperationSideEffect.ProjectSave, UcliOperationSideEffectValues.ProjectSave),
        (UcliOperationSideEffect.ExternalProcess, UcliOperationSideEffectValues.ExternalProcess),
        (UcliOperationSideEffect.FilesystemWrite, UcliOperationSideEffectValues.FilesystemWrite),
        (UcliOperationSideEffect.ArbitrarySourceExecution, UcliOperationSideEffectValues.ArbitrarySourceExecution),
        (UcliOperationSideEffect.DestructiveScope, UcliOperationSideEffectValues.DestructiveScope),
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

    /// <summary> Tries to parse a contract literal to one side-effect enum value. </summary>
    /// <param name="value"> The contract literal value. </param>
    /// <param name="sideEffect"> The parsed side-effect enum value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out UcliOperationSideEffect sideEffect)
    {
        return LiteralCodecUtilities.TryParse(
            value,
            Mappings,
            StringComparison.Ordinal,
            out sideEffect);
    }
}
