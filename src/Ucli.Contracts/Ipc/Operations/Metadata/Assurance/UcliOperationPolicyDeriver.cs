using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Derives operation admission policy from operation contract facts. </summary>
public static class UcliOperationPolicyDeriver
{
    /// <summary> Derives operation policy from assurance and code contract metadata. </summary>
    /// <param name="assurance"> The assurance metadata. </param>
    /// <param name="codeContract"> The optional source-code execution contract. </param>
    /// <returns> The derived operation policy. </returns>
    /// <exception cref="ArgumentException"> Thrown when policy cannot be derived from invalid contract facts. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="assurance" /> is <see langword="null" />. </exception>
    public static OperationPolicy Derive (
        UcliOperationAssuranceContract assurance,
        UcliOperationCodeContract? codeContract)
    {
        if (assurance == null)
        {
            throw new ArgumentNullException(nameof(assurance));
        }

        if (!TryDerive(assurance, codeContract, out var policy, out var errorMessage))
        {
            throw new ArgumentException(errorMessage, nameof(assurance));
        }

        return policy;
    }

    /// <summary> Tries to derive operation policy from assurance and code contract metadata. </summary>
    /// <param name="assurance"> The assurance metadata. </param>
    /// <param name="codeContract"> The optional source-code execution contract. </param>
    /// <param name="policy"> The derived operation policy. </param>
    /// <param name="errorMessage"> The derivation failure message. </param>
    /// <returns> <see langword="true" /> when policy derivation succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryDerive (
        UcliOperationAssuranceContract? assurance,
        UcliOperationCodeContract? codeContract,
        out OperationPolicy policy,
        out string errorMessage)
    {
        policy = OperationPolicy.Safe;

        if (assurance == null)
        {
            errorMessage = "Assurance metadata is required.";
            return false;
        }

        if (assurance.SideEffects == null)
        {
            errorMessage = "Assurance sideEffects are required.";
            return false;
        }

        var hasArbitrarySourceExecution = false;
        for (var i = 0; i < assurance.SideEffects.Count; i++)
        {
            var sideEffect = assurance.SideEffects[i];
            if (!UcliOperationSideEffectDescriptors.TryGetMinimumPolicy(sideEffect, out var sideEffectPolicy))
            {
                errorMessage = $"Unsupported operation side effect '{sideEffect}'.";
                return false;
            }

            if (string.Equals(sideEffect, UcliOperationSideEffectValues.ArbitrarySourceExecution, StringComparison.Ordinal))
            {
                hasArbitrarySourceExecution = true;
            }

            policy = Max(policy, sideEffectPolicy);
        }

        if (assurance.MayDirty || assurance.MayPersist)
        {
            policy = Max(policy, OperationPolicy.Advanced);
        }

        if (string.Equals(assurance.PlanMode, UcliOperationPlanModeValues.MayCreatePreviewState, StringComparison.Ordinal))
        {
            policy = Max(policy, OperationPolicy.Advanced);
        }

        if (codeContract != null && !hasArbitrarySourceExecution)
        {
            errorMessage = $"Operations with codeContract must declare sideEffects value '{UcliOperationSideEffectValues.ArbitrarySourceExecution}'.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static OperationPolicy Max (
        OperationPolicy left,
        OperationPolicy right)
    {
        return left >= right ? left : right;
    }
}
