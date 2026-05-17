namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Defines assurance claim codes emitted by the <c>ready</c> command. </summary>
internal static class ReadyClaimCodes
{
    public static readonly AssuranceClaimCode UnityReadyExecution = new("UNITY_READY_EXECUTION");

    public static readonly AssuranceClaimCode UnityReadyMutation = new("UNITY_READY_MUTATION");

    public static readonly AssuranceClaimCode UnityReadyTest = new("UNITY_READY_TEST");

    public static readonly AssuranceClaimCode UnityReadyReadIndex = new("UNITY_READY_READ_INDEX");

    public static IReadOnlyList<AssuranceClaimCode> All { get; } =
    [
        UnityReadyExecution,
        UnityReadyMutation,
        UnityReadyTest,
        UnityReadyReadIndex,
    ];

    /// <summary> Gets the claim code for one ready target. </summary>
    public static AssuranceClaimCode ForTarget (ReadyTarget target)
    {
        return target switch
        {
            ReadyTarget.Execution => UnityReadyExecution,
            ReadyTarget.Mutation => UnityReadyMutation,
            ReadyTarget.Test => UnityReadyTest,
            ReadyTarget.ReadIndex => UnityReadyReadIndex,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported ready target."),
        };
    }
}
