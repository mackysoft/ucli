using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines the minimum operation policy for each operation side-effect literal. </summary>
public static class UcliOperationSideEffectPolicyMatrix
{
    private static readonly IReadOnlyList<string> SupportedValuesCore = Array.AsReadOnly(new[]
    {
        UcliOperationSideEffectValues.ObservesUnityState,
        UcliOperationSideEffectValues.EditorStateChange,
        UcliOperationSideEffectValues.OpensSceneInEditor,
        UcliOperationSideEffectValues.OpensPrefabStage,
        UcliOperationSideEffectValues.AssetDatabaseRefresh,
        UcliOperationSideEffectValues.AssetImport,
        UcliOperationSideEffectValues.ScriptCompilation,
        UcliOperationSideEffectValues.DomainReload,
        UcliOperationSideEffectValues.SceneContentMutation,
        UcliOperationSideEffectValues.PrefabContentMutation,
        UcliOperationSideEffectValues.AssetContentMutation,
        UcliOperationSideEffectValues.ProjectSettingsMutation,
        UcliOperationSideEffectValues.SceneSave,
        UcliOperationSideEffectValues.PrefabSave,
        UcliOperationSideEffectValues.AssetSave,
        UcliOperationSideEffectValues.ProjectSave,
        UcliOperationSideEffectValues.ExternalProcess,
        UcliOperationSideEffectValues.FilesystemWrite,
        UcliOperationSideEffectValues.ArbitrarySourceExecution,
        UcliOperationSideEffectValues.DestructiveScope,
    });

    /// <summary> Gets all supported side-effect literals in canonical schema order. </summary>
    public static IReadOnlyList<string> SupportedValues => SupportedValuesCore;

    /// <summary> Tries to resolve the minimum policy for a side-effect literal. </summary>
    /// <param name="sideEffect"> The side-effect literal. </param>
    /// <param name="minimumPolicy"> The resolved minimum policy. </param>
    /// <returns> <see langword="true" /> when <paramref name="sideEffect" /> is supported; otherwise <see langword="false" />. </returns>
    public static bool TryGetMinimumPolicy (
        string? sideEffect,
        out OperationPolicy minimumPolicy)
    {
        switch (sideEffect)
        {
            case UcliOperationSideEffectValues.ObservesUnityState:
                minimumPolicy = OperationPolicy.Safe;
                return true;

            case UcliOperationSideEffectValues.EditorStateChange:
            case UcliOperationSideEffectValues.OpensSceneInEditor:
            case UcliOperationSideEffectValues.OpensPrefabStage:
            case UcliOperationSideEffectValues.AssetDatabaseRefresh:
            case UcliOperationSideEffectValues.AssetImport:
            case UcliOperationSideEffectValues.ScriptCompilation:
            case UcliOperationSideEffectValues.DomainReload:
            case UcliOperationSideEffectValues.SceneContentMutation:
            case UcliOperationSideEffectValues.PrefabContentMutation:
            case UcliOperationSideEffectValues.AssetContentMutation:
            case UcliOperationSideEffectValues.ProjectSettingsMutation:
            case UcliOperationSideEffectValues.SceneSave:
            case UcliOperationSideEffectValues.PrefabSave:
            case UcliOperationSideEffectValues.AssetSave:
            case UcliOperationSideEffectValues.ProjectSave:
                minimumPolicy = OperationPolicy.Advanced;
                return true;

            case UcliOperationSideEffectValues.ExternalProcess:
            case UcliOperationSideEffectValues.FilesystemWrite:
            case UcliOperationSideEffectValues.ArbitrarySourceExecution:
            case UcliOperationSideEffectValues.DestructiveScope:
                minimumPolicy = OperationPolicy.Dangerous;
                return true;

            default:
                minimumPolicy = OperationPolicy.Safe;
                return false;
        }
    }

    /// <summary> Gets a value indicating whether the side effect directly derives <see cref="OperationPolicy.Dangerous" />. </summary>
    /// <param name="sideEffect"> The side-effect literal. </param>
    /// <returns> <see langword="true" /> when the side effect is a dangerous derivation source; otherwise <see langword="false" />. </returns>
    public static bool IsDangerousDerivationSource (string? sideEffect)
    {
        switch (sideEffect)
        {
            case UcliOperationSideEffectValues.ExternalProcess:
            case UcliOperationSideEffectValues.FilesystemWrite:
            case UcliOperationSideEffectValues.ArbitrarySourceExecution:
            case UcliOperationSideEffectValues.DestructiveScope:
                return true;

            default:
                return false;
        }
    }
}
