namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Defines assurance claim codes emitted by the <c>ready</c> command. </summary>
internal static class ReadyClaimCodes
{
    public static readonly UcliCodeValue UnityReadyExecution = new("UNITY_READY_EXECUTION");

    public static readonly UcliCodeValue UnityReadyMutation = new("UNITY_READY_MUTATION");

    public static readonly UcliCodeValue UnityReadyTest = new("UNITY_READY_TEST");

    public static readonly UcliCodeValue UnityReadyReadIndex = new("UNITY_READY_READ_INDEX");

    public static IReadOnlyList<UcliCodeValue> All { get; } =
    [
        UnityReadyExecution,
        UnityReadyMutation,
        UnityReadyTest,
        UnityReadyReadIndex,
    ];

    /// <summary> Gets the claim code for one ready target. </summary>
    public static UcliCodeValue ForTarget (ReadyTarget target)
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
