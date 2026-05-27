using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Operations;

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Validates operation assurance metadata and derives operation policy. </summary>
internal static class UcliOperationAssuranceContractValidator
{
    private static readonly HashSet<string> SupportedPlanModes = new(StringComparer.Ordinal)
    {
        ContractLiteralCodec.ToValue(UcliOperationPlanMode.ValidationOnly),
        ContractLiteralCodec.ToValue(UcliOperationPlanMode.ObservesLiveUnity),
        ContractLiteralCodec.ToValue(UcliOperationPlanMode.MayCreatePreviewState),
    };

    private static readonly HashSet<string> SupportedTouchedKinds = new(StringComparer.Ordinal)
    {
        UcliTouchedResourceKindNames.Scene,
        UcliTouchedResourceKindNames.Prefab,
        UcliTouchedResourceKindNames.Asset,
        UcliTouchedResourceKindNames.ProjectSettings,
    };

    public static bool TryValidate (
        UcliOperationAssuranceContract? assurance,
        string? operationKind,
        string? operationPolicy,
        UcliOperationCodeContract? codeContract,
        string ownerName,
        bool allowMayCreatePreviewState,
        out OperationPolicy derivedPolicy,
        out string errorMessage)
    {
        derivedPolicy = OperationPolicy.Safe;
        if (!TryValidateRoot(assurance, ownerName, out errorMessage)
            || !TryValidatePlanMode(assurance!, ownerName, allowMayCreatePreviewState, out errorMessage)
            || !TryValidateVocabularies(assurance!, ownerName, out errorMessage)
            || !TryValidateConsistency(assurance!, operationKind, operationPolicy, codeContract, ownerName, out derivedPolicy, out errorMessage))
        {
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateRoot (
        UcliOperationAssuranceContract? assurance,
        string ownerName,
        out string errorMessage)
    {
        if (assurance == null
            || assurance.SideEffects == null
            || assurance.TouchedKinds == null
            || assurance.DangerousNotes == null
            || !SupportedPlanModes.Contains(assurance.PlanMode ?? string.Empty)
            || string.IsNullOrWhiteSpace(assurance.PlanSemantics)
            || string.IsNullOrWhiteSpace(assurance.CallSemantics)
            || string.IsNullOrWhiteSpace(assurance.TouchedContract)
            || string.IsNullOrWhiteSpace(assurance.ReadPostconditionContract)
            || string.IsNullOrWhiteSpace(assurance.FailureSemantics))
        {
            errorMessage = $"{ownerName} has invalid assurance metadata.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidatePlanMode (
        UcliOperationAssuranceContract assurance,
        string ownerName,
        bool allowMayCreatePreviewState,
        out string errorMessage)
    {
        if (!allowMayCreatePreviewState
            && string.Equals(assurance.PlanMode, ContractLiteralCodec.ToValue(UcliOperationPlanMode.MayCreatePreviewState), StringComparison.Ordinal))
        {
            errorMessage = $"{ownerName} public raw assurance metadata must not use planMode '{ContractLiteralCodec.ToValue(UcliOperationPlanMode.MayCreatePreviewState)}'.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateVocabularies (
        UcliOperationAssuranceContract assurance,
        string ownerName,
        out string errorMessage)
    {
        return TryValidateSideEffects(assurance.SideEffects!, ownerName, out errorMessage)
            && TryValidateTouchedKinds(assurance.TouchedKinds!, ownerName, out errorMessage)
            && TryValidateDangerousNotes(assurance.DangerousNotes!, ownerName, out errorMessage);
    }

    private static bool TryValidateSideEffects (
        IReadOnlyList<string> sideEffects,
        string ownerName,
        out string errorMessage)
    {
        for (var i = 0; i < sideEffects.Count; i++)
        {
            if (!UcliOperationSideEffectDescriptors.TryGetMinimumPolicy(sideEffects[i], out _))
            {
                errorMessage = $"{ownerName} has an unsupported side effect '{sideEffects[i]}'.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateTouchedKinds (
        IReadOnlyList<string> touchedKinds,
        string ownerName,
        out string errorMessage)
    {
        for (var i = 0; i < touchedKinds.Count; i++)
        {
            if (!SupportedTouchedKinds.Contains(touchedKinds[i]))
            {
                errorMessage = $"{ownerName} has an unsupported touched kind '{touchedKinds[i]}'.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateDangerousNotes (
        IReadOnlyList<string> dangerousNotes,
        string ownerName,
        out string errorMessage)
    {
        for (var i = 0; i < dangerousNotes.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(dangerousNotes[i]))
            {
                errorMessage = $"{ownerName} has an invalid dangerous note.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateConsistency (
        UcliOperationAssuranceContract assurance,
        string? operationKind,
        string? operationPolicy,
        UcliOperationCodeContract? codeContract,
        string ownerName,
        out OperationPolicy derivedPolicy,
        out string errorMessage)
    {
        derivedPolicy = OperationPolicy.Safe;
        return TryValidateOperationKind(assurance, operationKind, codeContract, ownerName, out errorMessage)
            && TryValidateProjectionAndConstraints(assurance, ownerName, out errorMessage)
            && TryValidateCodeContractPolicyFact(assurance, codeContract, ownerName, out errorMessage)
            && TryDerivePolicy(assurance, codeContract, ownerName, out derivedPolicy, out errorMessage)
            && TryValidateDeclaredPolicy(operationPolicy, derivedPolicy, ownerName, out errorMessage)
            && TryValidateDangerousNotesForPolicy(assurance, derivedPolicy, ownerName, out errorMessage);
    }

    private static bool TryValidateOperationKind (
        UcliOperationAssuranceContract assurance,
        string? operationKind,
        UcliOperationCodeContract? codeContract,
        string ownerName,
        out string errorMessage)
    {
        if (operationKind == null)
        {
            errorMessage = string.Empty;
            return true;
        }

        return TryValidateParsedOperationKind(assurance, operationKind, codeContract, ownerName, out errorMessage);
    }

    private static bool TryValidateParsedOperationKind (
        UcliOperationAssuranceContract assurance,
        string operationKind,
        UcliOperationCodeContract? codeContract,
        string ownerName,
        out string errorMessage)
    {
        if (!ContractLiteralInputParser.TryParseIgnoreCase<UcliOperationKind>(operationKind, out var parsedKind))
        {
            errorMessage = $"{ownerName} has unsupported operation kind metadata.";
            return false;
        }

        if (parsedKind == UcliOperationKind.Query)
        {
            return TryValidateQueryAssurance(assurance, codeContract, ownerName, out errorMessage);
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateQueryAssurance (
        UcliOperationAssuranceContract assurance,
        UcliOperationCodeContract? codeContract,
        string ownerName,
        out string errorMessage)
    {
        if (codeContract != null || assurance.MayDirty || assurance.MayPersist || assurance.TouchedKinds!.Count != 0 || !HasOnlyQuerySideEffects(assurance.SideEffects!))
        {
            errorMessage = $"{ownerName} has query assurance metadata with mutation or side-effect risk.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateCodeContractPolicyFact (
        UcliOperationAssuranceContract assurance,
        UcliOperationCodeContract? codeContract,
        string ownerName,
        out string errorMessage)
    {
        if (codeContract == null || Contains(assurance.SideEffects!, ContractLiteralCodec.ToValue(UcliOperationSideEffect.ArbitrarySourceExecution)))
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = $"{ownerName} codeContract requires assurance.sideEffects to include '{ContractLiteralCodec.ToValue(UcliOperationSideEffect.ArbitrarySourceExecution)}'.";
        return false;
    }

    private static bool TryDerivePolicy (
        UcliOperationAssuranceContract assurance,
        UcliOperationCodeContract? codeContract,
        string ownerName,
        out OperationPolicy derivedPolicy,
        out string errorMessage)
    {
        if (!UcliOperationPolicyDeriver.TryDerive(assurance, codeContract, out derivedPolicy))
        {
            errorMessage = $"{ownerName} has invalid policy derivation metadata.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateDeclaredPolicy (
        string? operationPolicy,
        OperationPolicy derivedPolicy,
        string ownerName,
        out string errorMessage)
    {
        if (operationPolicy == null)
        {
            errorMessage = string.Empty;
            return true;
        }

        return TryValidateParsedPolicy(operationPolicy, derivedPolicy, ownerName, out errorMessage);
    }

    private static bool TryValidateParsedPolicy (
        string operationPolicy,
        OperationPolicy derivedPolicy,
        string ownerName,
        out string errorMessage)
    {
        if (!ContractLiteralInputParser.TryParseIgnoreCase<OperationPolicy>(operationPolicy, out var parsedPolicy))
        {
            errorMessage = $"{ownerName} has unsupported operation policy metadata.";
            return false;
        }

        if (parsedPolicy == derivedPolicy)
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = $"{ownerName} policy '{operationPolicy}' does not match derived policy '{ContractLiteralCodec.ToValue(derivedPolicy)}'.";
        return false;
    }

    private static bool TryValidateDangerousNotesForPolicy (
        UcliOperationAssuranceContract assurance,
        OperationPolicy derivedPolicy,
        string ownerName,
        out string errorMessage)
    {
        if (derivedPolicy != OperationPolicy.Safe && assurance.DangerousNotes!.Count == 0)
        {
            errorMessage = $"{ownerName} must declare dangerousNotes for advanced or dangerous policy.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateProjectionAndConstraints (
        UcliOperationAssuranceContract assurance,
        string ownerName,
        out string errorMessage)
    {
        if (!TryValidateProjection(assurance, ownerName, out var derivedMayPersist, out errorMessage))
        {
            return false;
        }

        return TryValidateRequiredTouchedKinds(assurance, ownerName, derivedMayPersist, out errorMessage);
    }

    private static bool TryValidateProjection (
        UcliOperationAssuranceContract assurance,
        string ownerName,
        out bool derivedMayPersist,
        out string errorMessage)
    {
        derivedMayPersist = false;
        if (!UcliOperationSideEffectDescriptors.TryDeriveAssuranceProjection(assurance.SideEffects, out var derivedMayDirty, out derivedMayPersist))
        {
            errorMessage = $"{ownerName} has invalid side-effect projection metadata.";
            return false;
        }

        return TryValidateProjectionValues(assurance, ownerName, derivedMayDirty, derivedMayPersist, out errorMessage);
    }

    private static bool TryValidateProjectionValues (
        UcliOperationAssuranceContract assurance,
        string ownerName,
        bool derivedMayDirty,
        bool derivedMayPersist,
        out string errorMessage)
    {
        if (assurance.MayDirty != derivedMayDirty)
        {
            errorMessage = $"{ownerName} assurance.mayDirty does not match derived projection '{FormatBoolean(derivedMayDirty)}'.";
            return false;
        }

        return TryValidateMayPersistProjection(assurance, ownerName, derivedMayPersist, out errorMessage);
    }

    private static bool TryValidateMayPersistProjection (
        UcliOperationAssuranceContract assurance,
        string ownerName,
        bool derivedMayPersist,
        out string errorMessage)
    {
        if (assurance.MayPersist != derivedMayPersist)
        {
            errorMessage = $"{ownerName} assurance.mayPersist does not match derived projection '{FormatBoolean(derivedMayPersist)}'.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateRequiredTouchedKinds (
        UcliOperationAssuranceContract assurance,
        string ownerName,
        bool derivedMayPersist,
        out string errorMessage)
    {
        if (derivedMayPersist && assurance.TouchedKinds!.Count == 0)
        {
            errorMessage = $"{ownerName} assurance.mayPersist requires assurance.touchedKinds to be non-empty.";
            return false;
        }

        return TryValidateDescriptorRequiredTouchedKinds(assurance, ownerName, out errorMessage);
    }

    private static bool TryValidateDescriptorRequiredTouchedKinds (
        UcliOperationAssuranceContract assurance,
        string ownerName,
        out string errorMessage)
    {
        for (var i = 0; i < assurance.SideEffects!.Count; i++)
        {
            if (!TryValidateSideEffectRequiredTouchedKinds(assurance, assurance.SideEffects[i], ownerName, out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateSideEffectRequiredTouchedKinds (
        UcliOperationAssuranceContract assurance,
        string sideEffect,
        string ownerName,
        out string errorMessage)
    {
        if (!UcliOperationSideEffectDescriptors.TryGetDescriptor(sideEffect, out var descriptor))
        {
            errorMessage = $"{ownerName} has an unsupported side effect '{sideEffect}'.";
            return false;
        }

        return TryValidateRequiredTouchedKinds(assurance, descriptor.RequiredTouchedKinds, sideEffect, ownerName, out errorMessage);
    }

    private static bool TryValidateRequiredTouchedKinds (
        UcliOperationAssuranceContract assurance,
        IReadOnlyList<string> requiredTouchedKinds,
        string sideEffect,
        string ownerName,
        out string errorMessage)
    {
        for (var i = 0; i < requiredTouchedKinds.Count; i++)
        {
            if (!Contains(assurance.TouchedKinds!, requiredTouchedKinds[i]))
            {
                errorMessage = $"{ownerName} side effect '{sideEffect}' requires assurance.touchedKinds to include '{requiredTouchedKinds[i]}'.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool HasOnlyQuerySideEffects (IReadOnlyList<string> sideEffects)
    {
        for (var i = 0; i < sideEffects.Count; i++)
        {
            if (!UcliOperationSideEffectDescriptors.IsAllowedForQueryOperation(sideEffects[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Contains (
        IReadOnlyList<string> values,
        string expected)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], expected, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatBoolean (bool value)
    {
        return value ? "true" : "false";
    }
}
