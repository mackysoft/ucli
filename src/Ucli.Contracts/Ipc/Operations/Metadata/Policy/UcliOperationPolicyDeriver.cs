using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Derives operation admission policy from operation contract facts. </summary>
internal static class UcliOperationPolicyDeriver
{
    /// <summary> Derives operation policy from operation contract facts. </summary>
    /// <param name="assurance"> The assurance metadata. </param>
    /// <param name="codeContract"> The optional source-code execution contract. </param>
    /// <returns> The derived operation policy. </returns>
    /// <exception cref="ArgumentException"> Thrown when policy cannot be derived from invalid contract facts. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="assurance" /> is <see langword="null" />. </exception>
    public static OperationPolicy Derive (
        UcliOperationAssuranceContract assurance,
        UcliOperationCodeContract? codeContract = null)
    {
        if (assurance == null)
        {
            throw new ArgumentNullException(nameof(assurance));
        }

        if (!TryDerive(assurance, codeContract, out var policy))
        {
            throw new ArgumentException("Policy cannot be derived from invalid assurance metadata.", nameof(assurance));
        }

        return policy;
    }

    /// <summary> Tries to derive operation policy from assurance metadata. </summary>
    /// <param name="assurance"> The assurance metadata. </param>
    /// <param name="policy"> The derived operation policy. </param>
    /// <returns> <see langword="true" /> when policy derivation succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryDerive (
        UcliOperationAssuranceContract? assurance,
        out OperationPolicy policy)
    {
        return TryDerive(assurance, codeContract: null, out policy);
    }

    /// <summary> Tries to derive operation policy from operation contract facts. </summary>
    /// <param name="assurance"> The assurance metadata. </param>
    /// <param name="codeContract"> The optional source-code execution contract. </param>
    /// <param name="policy"> The derived operation policy. </param>
    /// <returns> <see langword="true" /> when policy derivation succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryDerive (
        UcliOperationAssuranceContract? assurance,
        UcliOperationCodeContract? codeContract,
        out OperationPolicy policy)
    {
        policy = OperationPolicy.Safe;

        if (assurance == null)
        {
            return false;
        }

        if (assurance.SideEffects == null)
        {
            return false;
        }

        if (!UcliOperationSideEffectDescriptors.TryDeriveAssuranceProjection(assurance.SideEffects, out var mayDirty, out var mayPersist))
        {
            return false;
        }

        for (var i = 0; i < assurance.SideEffects.Count; i++)
        {
            var sideEffect = assurance.SideEffects[i];
            if (!UcliOperationSideEffectDescriptors.TryGetMinimumPolicy(sideEffect, out var sideEffectPolicy))
            {
                return false;
            }

            policy = Max(policy, sideEffectPolicy);
        }

        if (mayDirty || mayPersist)
        {
            policy = Max(policy, OperationPolicy.Advanced);
        }

        if (string.Equals(assurance.PlanMode, UcliOperationPlanModeValues.MayCreatePreviewState, StringComparison.Ordinal))
        {
            policy = Max(policy, OperationPolicy.Advanced);
        }

        if (codeContract != null)
        {
            policy = Max(policy, OperationPolicy.Dangerous);
        }

        return true;
    }

    private static OperationPolicy Max (
        OperationPolicy left,
        OperationPolicy right)
    {
        return left >= right ? left : right;
    }
}
