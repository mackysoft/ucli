using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Validates contextual operation assurance rules and derives operation policy. </summary>
internal static class UcliOperationAssuranceContractValidator
{
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
        if (assurance == null)
        {
            errorMessage = $"{ownerName} has invalid assurance metadata.";
            return false;
        }

        if (!allowMayCreatePreviewState
            && assurance.PlanMode == UcliOperationPlanMode.MayCreatePreviewState)
        {
            errorMessage = $"{ownerName} public raw assurance metadata must not use planMode '{TextVocabulary.GetText(UcliOperationPlanMode.MayCreatePreviewState)}'.";
            return false;
        }

        if (!TryValidateOperationKind(assurance, operationKind, codeContract, ownerName, out errorMessage)
            || !TryValidateCodeContractFact(assurance, codeContract, ownerName, out errorMessage))
        {
            return false;
        }

        derivedPolicy = UcliOperationPolicyDeriver.Derive(assurance, codeContract);
        if (!TryValidateDeclaredPolicy(operationPolicy, derivedPolicy, ownerName, out errorMessage))
        {
            return false;
        }

        if (derivedPolicy != OperationPolicy.Safe && assurance.DangerousNotes.Count == 0)
        {
            errorMessage = $"{ownerName} must declare dangerousNotes for advanced or dangerous policy.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
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

        if (!VocabularyInputParser.TryParseIgnoreCase<UcliOperationKind>(operationKind, out var parsedKind))
        {
            errorMessage = $"{ownerName} has unsupported operation kind metadata.";
            return false;
        }

        if (parsedKind == UcliOperationKind.Query
            && (codeContract != null
                || assurance.MayDirty
                || assurance.MayPersist
                || assurance.TouchedKinds.Count != 0
                || !HasOnlyQuerySideEffects(assurance.SideEffects)))
        {
            errorMessage = $"{ownerName} has query assurance metadata with mutation or side-effect risk.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateCodeContractFact (
        UcliOperationAssuranceContract assurance,
        UcliOperationCodeContract? codeContract,
        string ownerName,
        out string errorMessage)
    {
        if (codeContract == null
            || assurance.SideEffects.Contains(UcliOperationSideEffect.ArbitrarySourceExecution))
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = $"{ownerName} codeContract requires assurance.sideEffects to include '{TextVocabulary.GetText(UcliOperationSideEffect.ArbitrarySourceExecution)}'.";
        return false;
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

        if (!VocabularyInputParser.TryParseIgnoreCase<OperationPolicy>(operationPolicy, out var parsedPolicy))
        {
            errorMessage = $"{ownerName} has unsupported operation policy metadata.";
            return false;
        }

        if (parsedPolicy != derivedPolicy)
        {
            errorMessage = $"{ownerName} policy '{operationPolicy}' does not match derived policy '{TextVocabulary.GetText(derivedPolicy)}'.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool HasOnlyQuerySideEffects (IReadOnlyList<UcliOperationSideEffect> sideEffects)
    {
        for (var index = 0; index < sideEffects.Count; index++)
        {
            if (!UcliOperationSideEffectDescriptors.GetDescriptor(sideEffects[index]).AllowedForQueryOperation)
            {
                return false;
            }
        }

        return true;
    }
}
