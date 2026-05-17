using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines the minimum operation policy for each operation side-effect literal. </summary>
public static class UcliOperationSideEffectPolicyMatrix
{
    private static readonly SideEffectPolicyDefinition[] Definitions =
    {
        Define(UcliOperationSideEffectValues.ObservesUnityState, OperationPolicy.Safe, queryAllowed: true),
        Define(UcliOperationSideEffectValues.EditorStateChange, OperationPolicy.Advanced),
        Define(UcliOperationSideEffectValues.OpensSceneInEditor, OperationPolicy.Advanced),
        Define(UcliOperationSideEffectValues.OpensPrefabStage, OperationPolicy.Advanced),
        Define(UcliOperationSideEffectValues.AssetDatabaseRefresh, OperationPolicy.Advanced),
        Define(UcliOperationSideEffectValues.AssetImport, OperationPolicy.Advanced),
        Define(UcliOperationSideEffectValues.ScriptCompilation, OperationPolicy.Advanced),
        Define(UcliOperationSideEffectValues.DomainReload, OperationPolicy.Advanced),
        Define(UcliOperationSideEffectValues.SceneContentMutation, OperationPolicy.Advanced),
        Define(UcliOperationSideEffectValues.PrefabContentMutation, OperationPolicy.Advanced),
        Define(UcliOperationSideEffectValues.AssetContentMutation, OperationPolicy.Advanced),
        Define(UcliOperationSideEffectValues.ProjectSettingsMutation, OperationPolicy.Advanced),
        Define(UcliOperationSideEffectValues.SceneSave, OperationPolicy.Advanced),
        Define(UcliOperationSideEffectValues.PrefabSave, OperationPolicy.Advanced),
        Define(UcliOperationSideEffectValues.AssetSave, OperationPolicy.Advanced),
        Define(UcliOperationSideEffectValues.ProjectSave, OperationPolicy.Advanced),
        Define(UcliOperationSideEffectValues.ExternalProcess, OperationPolicy.Dangerous),
        Define(UcliOperationSideEffectValues.FilesystemWrite, OperationPolicy.Dangerous),
        Define(UcliOperationSideEffectValues.ArbitrarySourceExecution, OperationPolicy.Dangerous),
        Define(UcliOperationSideEffectValues.DestructiveScope, OperationPolicy.Dangerous),
    };

    private static readonly IReadOnlyList<string> SupportedValuesCore = Array.AsReadOnly(CreateSupportedValues());

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
        if (TryGetDefinition(sideEffect, out var definition))
        {
            minimumPolicy = definition.MinimumPolicy;
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
        return TryGetDefinition(sideEffect, out var definition)
            && definition.MinimumPolicy == OperationPolicy.Dangerous;
    }

    /// <summary> Gets a value indicating whether the side effect can be declared by a query operation. </summary>
    /// <param name="sideEffect"> The side-effect literal. </param>
    /// <returns> <see langword="true" /> when the side effect is query-compatible; otherwise <see langword="false" />. </returns>
    public static bool IsAllowedForQuery (string? sideEffect)
    {
        return TryGetDefinition(sideEffect, out var definition)
            && definition.QueryAllowed;
    }

    private static SideEffectPolicyDefinition Define (
        string value,
        OperationPolicy minimumPolicy,
        bool queryAllowed = false)
    {
        return new SideEffectPolicyDefinition(
            value,
            minimumPolicy,
            queryAllowed);
    }

    private static string[] CreateSupportedValues ()
    {
        var values = new string[Definitions.Length];
        for (var i = 0; i < Definitions.Length; i++)
        {
            values[i] = Definitions[i].Value;
        }

        return values;
    }

    private static bool TryGetDefinition (
        string? sideEffect,
        out SideEffectPolicyDefinition definition)
    {
        for (var i = 0; i < Definitions.Length; i++)
        {
            if (string.Equals(Definitions[i].Value, sideEffect, StringComparison.Ordinal))
            {
                definition = Definitions[i];
                return true;
            }
        }

        definition = default;
        return false;
    }

    private readonly struct SideEffectPolicyDefinition
    {
        public SideEffectPolicyDefinition (
            string value,
            OperationPolicy minimumPolicy,
            bool queryAllowed)
        {
            Value = value;
            MinimumPolicy = minimumPolicy;
            QueryAllowed = queryAllowed;
        }

        public string Value { get; }

        public OperationPolicy MinimumPolicy { get; }

        public bool QueryAllowed { get; }
    }
}
