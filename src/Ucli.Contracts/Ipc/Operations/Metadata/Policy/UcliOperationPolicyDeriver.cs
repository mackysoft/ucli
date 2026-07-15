using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Derives operation admission policy from validated operation contract facts. </summary>
internal static class UcliOperationPolicyDeriver
{
    /// <summary> Derives operation policy from operation contract facts. </summary>
    /// <param name="assurance"> The validated assurance metadata. </param>
    /// <param name="codeContract"> The source-code execution contract, or <see langword="null" /> when absent. </param>
    /// <returns> The derived operation policy. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="assurance" /> is <see langword="null" />. </exception>
    public static OperationPolicy Derive (
        UcliOperationAssuranceContract assurance,
        UcliOperationCodeContract? codeContract)
    {
        if (assurance == null)
        {
            throw new ArgumentNullException(nameof(assurance));
        }

        var policy = OperationPolicy.Safe;
        for (var index = 0; index < assurance.SideEffects.Count; index++)
        {
            var descriptor = UcliOperationSideEffectDescriptors.GetDescriptor(assurance.SideEffects[index]);
            policy = Max(policy, descriptor.MinimumPolicy);
        }

        if (assurance.PlanMode == UcliOperationPlanMode.MayCreatePreviewState)
        {
            policy = Max(policy, OperationPolicy.Advanced);
        }

        if (codeContract != null)
        {
            policy = OperationPolicy.Dangerous;
        }

        return policy;
    }

    private static OperationPolicy Max (
        OperationPolicy left,
        OperationPolicy right)
    {
        return left >= right ? left : right;
    }
}
