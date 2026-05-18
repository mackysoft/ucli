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
            requiredTouchedKinds: new[] { IpcExecuteTouchedResourceKindNames.Scene }),
        Define(
            UcliOperationSideEffect.OpensPrefabStage,
            OperationPolicy.Advanced,
            requiredTouchedKinds: new[] { IpcExecuteTouchedResourceKindNames.Prefab }),
        Define(
            UcliOperationSideEffect.AssetDatabaseRefresh,
            OperationPolicy.Advanced,
            requiredTouchedKinds: new[] { IpcExecuteTouchedResourceKindNames.Asset }),
        Define(
            UcliOperationSideEffect.AssetImport,
            OperationPolicy.Advanced,
            requiredTouchedKinds: new[] { IpcExecuteTouchedResourceKindNames.Asset }),
        Define(UcliOperationSideEffect.ScriptCompilation, OperationPolicy.Advanced),
        Define(UcliOperationSideEffect.DomainReload, OperationPolicy.Advanced),
        Define(
            UcliOperationSideEffect.SceneContentMutation,
            OperationPolicy.Advanced,
            derivesMayDirty: true,
            requiredTouchedKinds: new[] { IpcExecuteTouchedResourceKindNames.Scene }),
        Define(
            UcliOperationSideEffect.PrefabContentMutation,
            OperationPolicy.Advanced,
            derivesMayDirty: true,
            requiredTouchedKinds: new[] { IpcExecuteTouchedResourceKindNames.Prefab }),
        Define(
            UcliOperationSideEffect.AssetContentMutation,
            OperationPolicy.Advanced,
            derivesMayDirty: true,
            requiredTouchedKinds: new[] { IpcExecuteTouchedResourceKindNames.Asset }),
        Define(
            UcliOperationSideEffect.ProjectSettingsMutation,
            OperationPolicy.Advanced,
            derivesMayDirty: true,
            requiredTouchedKinds: new[] { IpcExecuteTouchedResourceKindNames.ProjectSettings }),
        Define(
            UcliOperationSideEffect.SceneSave,
            OperationPolicy.Advanced,
            derivesMayPersist: true,
            requiredTouchedKinds: new[] { IpcExecuteTouchedResourceKindNames.Scene }),
        Define(
            UcliOperationSideEffect.PrefabSave,
            OperationPolicy.Advanced,
            derivesMayPersist: true,
            requiredTouchedKinds: new[] { IpcExecuteTouchedResourceKindNames.Prefab }),
        Define(
            UcliOperationSideEffect.AssetSave,
            OperationPolicy.Advanced,
            derivesMayPersist: true,
            requiredTouchedKinds: new[] { IpcExecuteTouchedResourceKindNames.Asset }),
        Define(
            UcliOperationSideEffect.ProjectSave,
            OperationPolicy.Advanced,
            derivesMayPersist: true,
            requiredTouchedKinds: new[]
            {
                IpcExecuteTouchedResourceKindNames.Scene,
                IpcExecuteTouchedResourceKindNames.Prefab,
                IpcExecuteTouchedResourceKindNames.Asset,
                IpcExecuteTouchedResourceKindNames.ProjectSettings,
            }),
        Define(UcliOperationSideEffect.ExternalProcess, OperationPolicy.Dangerous),
        Define(
            UcliOperationSideEffect.FilesystemWrite,
            OperationPolicy.Dangerous,
            derivesMayPersist: true),
        Define(UcliOperationSideEffect.ArbitrarySourceExecution, OperationPolicy.Dangerous),
        Define(UcliOperationSideEffect.DestructiveScope, OperationPolicy.Dangerous),
    };

    private static readonly IReadOnlyList<UcliOperationSideEffectDescriptor> AllCore = Array.AsReadOnly(DescriptorsCore);

    private static readonly IReadOnlyList<string> SupportedValuesCore = Array.AsReadOnly(DescriptorsCore
        .Select(static descriptor => descriptor.Value)
        .ToArray());

    /// <summary> Gets all side-effect descriptors in canonical schema order. </summary>
    internal static IReadOnlyList<UcliOperationSideEffectDescriptor> All => AllCore;

    /// <summary> Gets all supported side-effect literals in canonical schema order. </summary>
    public static IReadOnlyList<string> SupportedValues => SupportedValuesCore;

    /// <summary> Tries to resolve the descriptor for a side-effect literal. </summary>
    /// <param name="sideEffect"> The side-effect literal. </param>
    /// <param name="descriptor"> The resolved descriptor. </param>
    /// <returns> <see langword="true" /> when <paramref name="sideEffect" /> is supported; otherwise <see langword="false" />. </returns>
    internal static bool TryGetDescriptor (
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
    internal static bool TryGetMinimumPolicy (
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
    internal static bool IsDangerousDerivationSource (string? sideEffect)
    {
        return TryGetDescriptor(sideEffect, out var descriptor)
            && descriptor.MinimumPolicy == OperationPolicy.Dangerous;
    }

    /// <summary> Gets a value indicating whether the side effect can be declared by a query operation. </summary>
    /// <param name="sideEffect"> The side-effect literal. </param>
    /// <returns> <see langword="true" /> when the side effect is query-compatible; otherwise <see langword="false" />. </returns>
    internal static bool IsAllowedForQueryOperation (string? sideEffect)
    {
        return TryGetDescriptor(sideEffect, out var descriptor)
            && descriptor.AllowedForQueryOperation;
    }

    /// <summary> Tries to derive <c>assurance.mayDirty</c> and <c>assurance.mayPersist</c> from side-effect literals. </summary>
    /// <param name="sideEffects"> The side-effect literals. </param>
    /// <param name="mayDirty"> The derived dirty-state projection. </param>
    /// <param name="mayPersist"> The derived persistence projection. </param>
    /// <returns> <see langword="true" /> when all side effects are supported; otherwise <see langword="false" />. </returns>
    internal static bool TryDeriveAssuranceProjection (
        IReadOnlyList<string>? sideEffects,
        out bool mayDirty,
        out bool mayPersist)
    {
        mayDirty = false;
        mayPersist = false;

        if (sideEffects == null)
        {
            return false;
        }

        for (var i = 0; i < sideEffects.Count; i++)
        {
            if (!TryGetDescriptor(sideEffects[i], out var descriptor))
            {
                mayDirty = false;
                mayPersist = false;
                return false;
            }

            mayDirty |= descriptor.DerivesMayDirty;
            mayPersist |= descriptor.DerivesMayPersist;
        }

        return true;
    }

    /// <summary> Tries to derive <c>assurance.mayDirty</c> and <c>assurance.mayPersist</c> from side-effect enum values. </summary>
    /// <param name="sideEffects"> The side-effect enum values. </param>
    /// <param name="mayDirty"> The derived dirty-state projection. </param>
    /// <param name="mayPersist"> The derived persistence projection. </param>
    /// <returns> <see langword="true" /> when <paramref name="sideEffects" /> is not <see langword="null" />; otherwise <see langword="false" />. </returns>
    internal static bool TryDeriveAssuranceProjection (
        IReadOnlyList<UcliOperationSideEffect>? sideEffects,
        out bool mayDirty,
        out bool mayPersist)
    {
        mayDirty = false;
        mayPersist = false;

        if (sideEffects == null)
        {
            return false;
        }

        for (var i = 0; i < sideEffects.Count; i++)
        {
            if (!TryGetDescriptor(UcliOperationSideEffectCodec.ToValue(sideEffects[i]), out var descriptor))
            {
                mayDirty = false;
                mayPersist = false;
                return false;
            }

            mayDirty |= descriptor.DerivesMayDirty;
            mayPersist |= descriptor.DerivesMayPersist;
        }

        return true;
    }

    private static UcliOperationSideEffectDescriptor Define (
        UcliOperationSideEffect sideEffect,
        OperationPolicy minimumPolicy,
        bool derivesMayDirty = false,
        bool derivesMayPersist = false,
        string[]? requiredTouchedKinds = null)
    {
        return new UcliOperationSideEffectDescriptor(
            sideEffect,
            minimumPolicy,
            derivesMayDirty,
            derivesMayPersist,
            allowedForQueryOperation: false,
            requiredTouchedKinds ?? Array.Empty<string>());
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
            Array.Empty<string>());
    }

}
