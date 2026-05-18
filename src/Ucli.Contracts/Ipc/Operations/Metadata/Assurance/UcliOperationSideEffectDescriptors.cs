using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Ipc;

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
            UcliOperationSideEffectRequiredAssuranceFact.TouchedKindIncludes(IpcExecuteTouchedResourceKindNames.Scene)),
        Define(
            UcliOperationSideEffect.OpensPrefabStage,
            OperationPolicy.Advanced,
            UcliOperationSideEffectRequiredAssuranceFact.TouchedKindIncludes(IpcExecuteTouchedResourceKindNames.Prefab)),
        Define(
            UcliOperationSideEffect.AssetDatabaseRefresh,
            OperationPolicy.Advanced,
            UcliOperationSideEffectRequiredAssuranceFact.TouchedKindIncludes(IpcExecuteTouchedResourceKindNames.Asset)),
        Define(
            UcliOperationSideEffect.AssetImport,
            OperationPolicy.Advanced,
            UcliOperationSideEffectRequiredAssuranceFact.TouchedKindIncludes(IpcExecuteTouchedResourceKindNames.Asset)),
        Define(UcliOperationSideEffect.ScriptCompilation, OperationPolicy.Advanced),
        Define(UcliOperationSideEffect.DomainReload, OperationPolicy.Advanced),
        Define(
            UcliOperationSideEffect.SceneContentMutation,
            OperationPolicy.Advanced,
            UcliOperationSideEffectRequiredAssuranceFact.MayDirtyTrue(),
            UcliOperationSideEffectRequiredAssuranceFact.TouchedKindIncludes(IpcExecuteTouchedResourceKindNames.Scene)),
        Define(
            UcliOperationSideEffect.PrefabContentMutation,
            OperationPolicy.Advanced,
            UcliOperationSideEffectRequiredAssuranceFact.MayDirtyTrue(),
            UcliOperationSideEffectRequiredAssuranceFact.TouchedKindIncludes(IpcExecuteTouchedResourceKindNames.Prefab)),
        Define(
            UcliOperationSideEffect.AssetContentMutation,
            OperationPolicy.Advanced,
            UcliOperationSideEffectRequiredAssuranceFact.MayDirtyTrue(),
            UcliOperationSideEffectRequiredAssuranceFact.TouchedKindIncludes(IpcExecuteTouchedResourceKindNames.Asset)),
        Define(
            UcliOperationSideEffect.ProjectSettingsMutation,
            OperationPolicy.Advanced,
            UcliOperationSideEffectRequiredAssuranceFact.MayDirtyTrue(),
            UcliOperationSideEffectRequiredAssuranceFact.TouchedKindIncludes(IpcExecuteTouchedResourceKindNames.ProjectSettings)),
        Define(
            UcliOperationSideEffect.SceneSave,
            OperationPolicy.Advanced,
            UcliOperationSideEffectRequiredAssuranceFact.MayPersistTrue(),
            UcliOperationSideEffectRequiredAssuranceFact.TouchedKindIncludes(IpcExecuteTouchedResourceKindNames.Scene)),
        Define(
            UcliOperationSideEffect.PrefabSave,
            OperationPolicy.Advanced,
            UcliOperationSideEffectRequiredAssuranceFact.MayPersistTrue(),
            UcliOperationSideEffectRequiredAssuranceFact.TouchedKindIncludes(IpcExecuteTouchedResourceKindNames.Prefab)),
        Define(
            UcliOperationSideEffect.AssetSave,
            OperationPolicy.Advanced,
            UcliOperationSideEffectRequiredAssuranceFact.MayPersistTrue(),
            UcliOperationSideEffectRequiredAssuranceFact.TouchedKindIncludes(IpcExecuteTouchedResourceKindNames.Asset)),
        Define(
            UcliOperationSideEffect.ProjectSave,
            OperationPolicy.Advanced,
            UcliOperationSideEffectRequiredAssuranceFact.MayPersistTrue(),
            UcliOperationSideEffectRequiredAssuranceFact.TouchedKindIncludes(IpcExecuteTouchedResourceKindNames.Scene),
            UcliOperationSideEffectRequiredAssuranceFact.TouchedKindIncludes(IpcExecuteTouchedResourceKindNames.Prefab),
            UcliOperationSideEffectRequiredAssuranceFact.TouchedKindIncludes(IpcExecuteTouchedResourceKindNames.Asset),
            UcliOperationSideEffectRequiredAssuranceFact.TouchedKindIncludes(IpcExecuteTouchedResourceKindNames.ProjectSettings)),
        Define(UcliOperationSideEffect.ExternalProcess, OperationPolicy.Dangerous),
        Define(
            UcliOperationSideEffect.FilesystemWrite,
            OperationPolicy.Dangerous,
            UcliOperationSideEffectRequiredAssuranceFact.MayPersistTrue()),
        Define(UcliOperationSideEffect.ArbitrarySourceExecution, OperationPolicy.Dangerous),
        Define(UcliOperationSideEffect.DestructiveScope, OperationPolicy.Dangerous),
    };

    private static readonly IReadOnlyList<UcliOperationSideEffectDescriptor> AllCore = Array.AsReadOnly(DescriptorsCore);

    private static readonly IReadOnlyList<string> SupportedValuesCore = Array.AsReadOnly(CreateSupportedValues());

    /// <summary> Gets all side-effect descriptors in canonical schema order. </summary>
    public static IReadOnlyList<UcliOperationSideEffectDescriptor> All => AllCore;

    /// <summary> Gets all supported side-effect literals in canonical schema order. </summary>
    public static IReadOnlyList<string> SupportedValues => SupportedValuesCore;

    /// <summary> Tries to resolve the descriptor for a side-effect literal. </summary>
    /// <param name="sideEffect"> The side-effect literal. </param>
    /// <param name="descriptor"> The resolved descriptor. </param>
    /// <returns> <see langword="true" /> when <paramref name="sideEffect" /> is supported; otherwise <see langword="false" />. </returns>
    public static bool TryGetDescriptor (
        string? sideEffect,
        [NotNullWhen(true)] out UcliOperationSideEffectDescriptor? descriptor)
    {
        for (var i = 0; i < DescriptorsCore.Length; i++)
        {
            if (string.Equals(DescriptorsCore[i].Value, sideEffect, StringComparison.Ordinal))
            {
                descriptor = DescriptorsCore[i];
                return true;
            }
        }

        descriptor = null;
        return false;
    }

    /// <summary> Tries to resolve the minimum policy for a side-effect literal. </summary>
    /// <param name="sideEffect"> The side-effect literal. </param>
    /// <param name="minimumPolicy"> The resolved minimum policy. </param>
    /// <returns> <see langword="true" /> when <paramref name="sideEffect" /> is supported; otherwise <see langword="false" />. </returns>
    public static bool TryGetMinimumPolicy (
        string? sideEffect,
        out OperationPolicy minimumPolicy)
    {
        if (TryGetDescriptor(sideEffect, out var descriptor))
        {
            minimumPolicy = descriptor.MinimumPolicy;
            return true;
        }

        minimumPolicy = OperationPolicy.Safe;
        return false;
    }

    /// <summary> Gets a value indicating whether the side effect directly derives <see cref="OperationPolicy.Dangerous" />. </summary>
    /// <param name="sideEffect"> The side-effect literal. </param>
    /// <returns> <see langword="true" /> when the side effect is a dangerous derivation source; otherwise <see langword="false" />. </returns>
    public static bool IsDangerousDerivationSource (string? sideEffect)
    {
        return TryGetDescriptor(sideEffect, out var descriptor)
            && descriptor.MinimumPolicy == OperationPolicy.Dangerous;
    }

    /// <summary> Gets a value indicating whether the side effect can be declared by a query operation. </summary>
    /// <param name="sideEffect"> The side-effect literal. </param>
    /// <returns> <see langword="true" /> when the side effect is query-compatible; otherwise <see langword="false" />. </returns>
    public static bool IsAllowedForQuery (string? sideEffect)
    {
        return TryGetDescriptor(sideEffect, out var descriptor)
            && descriptor.QueryAllowed;
    }

    private static UcliOperationSideEffectDescriptor Define (
        UcliOperationSideEffect sideEffect,
        OperationPolicy minimumPolicy,
        params UcliOperationSideEffectRequiredAssuranceFact[] requiredAssuranceFacts)
    {
        return new UcliOperationSideEffectDescriptor(
            sideEffect,
            minimumPolicy,
            queryAllowed: false,
            requiredAssuranceFacts);
    }

    private static UcliOperationSideEffectDescriptor DefineQuery (
        UcliOperationSideEffect sideEffect,
        OperationPolicy minimumPolicy)
    {
        return new UcliOperationSideEffectDescriptor(
            sideEffect,
            minimumPolicy,
            queryAllowed: true,
            Array.Empty<UcliOperationSideEffectRequiredAssuranceFact>());
    }

    private static string[] CreateSupportedValues ()
    {
        var values = new string[DescriptorsCore.Length];
        for (var i = 0; i < DescriptorsCore.Length; i++)
        {
            values[i] = DescriptorsCore[i].Value;
        }

        return values;
    }
}
