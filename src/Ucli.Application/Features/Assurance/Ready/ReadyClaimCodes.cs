namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Defines assurance claim codes emitted by the <c>ready</c> command. </summary>
internal static class ReadyClaimCodes
{
    public const string UnityReadyExecution = "UNITY_READY_EXECUTION";

    public const string UnityReadyMutation = "UNITY_READY_MUTATION";

    public const string UnityReadyTest = "UNITY_READY_TEST";

    public const string UnityReadyReadIndex = "UNITY_READY_READ_INDEX";

    public static IReadOnlyList<string> All { get; } =
    [
        UnityReadyExecution,
        UnityReadyMutation,
        UnityReadyTest,
        UnityReadyReadIndex,
    ];

    /// <summary> Gets the claim code for one ready target. </summary>
    public static string ForTarget (ReadyTarget target)
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
