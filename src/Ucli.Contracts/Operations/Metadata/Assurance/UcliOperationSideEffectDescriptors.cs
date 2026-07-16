using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Defines the closed descriptor vocabulary for operation side-effect literals. </summary>
public static class UcliOperationSideEffectDescriptors
{
    private static readonly UcliOperationSideEffectDescriptor[] DescriptorsCore =
    {
        DefineQuery(UcliOperationSideEffect.ObservesUnityState, OperationPolicy.Safe),
        Define(UcliOperationSideEffect.EditorStateChange, OperationPolicy.Advanced),
        Define(
            UcliOperationSideEffect.OpensSceneInEditor,
            OperationPolicy.Advanced,
            requiredTouchedKinds: new[] { UcliTouchedResourceKind.Scene }),
        Define(
            UcliOperationSideEffect.OpensPrefabStage,
            OperationPolicy.Advanced,
            requiredTouchedKinds: new[] { UcliTouchedResourceKind.Prefab }),
        Define(
            UcliOperationSideEffect.AssetDatabaseRefresh,
            OperationPolicy.Advanced,
            requiredTouchedKinds: new[] { UcliTouchedResourceKind.Asset }),
        Define(
            UcliOperationSideEffect.AssetImport,
            OperationPolicy.Advanced,
            requiredTouchedKinds: new[] { UcliTouchedResourceKind.Asset }),
        Define(UcliOperationSideEffect.ScriptCompilation, OperationPolicy.Advanced),
        Define(UcliOperationSideEffect.DomainReload, OperationPolicy.Advanced),
        Define(
            UcliOperationSideEffect.SceneContentMutation,
            OperationPolicy.Advanced,
            derivesMayDirty: true,
            requiredTouchedKinds: new[] { UcliTouchedResourceKind.Scene }),
        Define(
            UcliOperationSideEffect.PrefabContentMutation,
            OperationPolicy.Advanced,
            derivesMayDirty: true,
            requiredTouchedKinds: new[] { UcliTouchedResourceKind.Prefab }),
        Define(
            UcliOperationSideEffect.AssetContentMutation,
            OperationPolicy.Advanced,
            derivesMayDirty: true,
            requiredTouchedKinds: new[] { UcliTouchedResourceKind.Asset }),
        Define(
            UcliOperationSideEffect.ProjectSettingsMutation,
            OperationPolicy.Advanced,
            derivesMayDirty: true,
            requiredTouchedKinds: new[] { UcliTouchedResourceKind.ProjectSettings }),
        Define(
            UcliOperationSideEffect.SceneSave,
            OperationPolicy.Advanced,
            derivesMayPersist: true,
            requiredTouchedKinds: new[] { UcliTouchedResourceKind.Scene }),
        Define(
            UcliOperationSideEffect.PrefabSave,
            OperationPolicy.Advanced,
            derivesMayPersist: true,
            requiredTouchedKinds: new[] { UcliTouchedResourceKind.Prefab }),
        Define(
            UcliOperationSideEffect.AssetSave,
            OperationPolicy.Advanced,
            derivesMayPersist: true,
            requiredTouchedKinds: new[] { UcliTouchedResourceKind.Asset }),
        Define(
            UcliOperationSideEffect.ProjectSave,
            OperationPolicy.Advanced,
            derivesMayPersist: true,
            requiredTouchedKinds: new[]
            {
                UcliTouchedResourceKind.Scene,
                UcliTouchedResourceKind.Prefab,
                UcliTouchedResourceKind.Asset,
                UcliTouchedResourceKind.ProjectSettings,
            }),
        Define(UcliOperationSideEffect.ExternalProcess, OperationPolicy.Dangerous),
        Define(
            UcliOperationSideEffect.FilesystemWrite,
            OperationPolicy.Dangerous,
            derivesMayPersist: true),
        Define(UcliOperationSideEffect.ArbitrarySourceExecution, OperationPolicy.Dangerous),
        Define(UcliOperationSideEffect.DestructiveScope, OperationPolicy.Dangerous),
        Define(
            UcliOperationSideEffect.RuntimeStateMutation,
            OperationPolicy.Advanced,
            derivesMayDirty: true),
    };

    private static readonly IReadOnlyList<UcliOperationSideEffectDescriptor> AllCore = Array.AsReadOnly(DescriptorsCore);

    private static readonly IReadOnlyList<string> SupportedValuesCore = Array.AsReadOnly(DescriptorsCore
        .Select(static descriptor => descriptor.Value)
        .ToArray());

    /// <summary> Gets all side-effect descriptors in canonical schema order. </summary>
    internal static IReadOnlyList<UcliOperationSideEffectDescriptor> All => AllCore;

    /// <summary> Gets all supported side-effect literals in canonical schema order. </summary>
    public static IReadOnlyList<string> SupportedValues => SupportedValuesCore;

    /// <summary> Gets the descriptor for a defined side-effect value. </summary>
    /// <exception cref="InvalidOperationException"> Thrown when the descriptor table does not cover the value. </exception>
    internal static UcliOperationSideEffectDescriptor GetDescriptor (UcliOperationSideEffect sideEffect)
    {
        for (var i = 0; i < DescriptorsCore.Length; i++)
        {
            if (DescriptorsCore[i].SideEffect == sideEffect)
            {
                return DescriptorsCore[i];
            }
        }

        throw new InvalidOperationException($"The side-effect descriptor table does not cover '{sideEffect}'.");
    }

    private static UcliOperationSideEffectDescriptor Define (
        UcliOperationSideEffect sideEffect,
        OperationPolicy minimumPolicy,
        bool derivesMayDirty = false,
        bool derivesMayPersist = false,
        UcliTouchedResourceKind[]? requiredTouchedKinds = null)
    {
        return new UcliOperationSideEffectDescriptor(
            sideEffect,
            minimumPolicy,
            derivesMayDirty,
            derivesMayPersist,
            allowedForQueryOperation: false,
            requiredTouchedKinds ?? Array.Empty<UcliTouchedResourceKind>());
    }

    private static UcliOperationSideEffectDescriptor DefineQuery (
        UcliOperationSideEffect sideEffect,
        OperationPolicy minimumPolicy)
    {
        return new UcliOperationSideEffectDescriptor(
            sideEffect,
            minimumPolicy,
            derivesMayDirty: false,
            derivesMayPersist: false,
            allowedForQueryOperation: true,
            Array.Empty<UcliTouchedResourceKind>());
    }

}
