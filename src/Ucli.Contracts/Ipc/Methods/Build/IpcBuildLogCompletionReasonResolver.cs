namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Resolves build log completion reason values from normalized BuildReport results. </summary>
internal static class IpcBuildLogCompletionReasonResolver
{
    /// <summary> Maps one normalized BuildReport result to the corresponding build log completion reason. </summary>
    /// <param name="result"> The normalized BuildReport result. </param>
    /// <returns> The completion reason implied by <paramref name="result" />. </returns>
    public static IpcBuildLogCompletionReason FromReportResult (IpcBuildReportResult result)
    {
        return result switch
        {
            IpcBuildReportResult.Succeeded => IpcBuildLogCompletionReason.Completed,
            IpcBuildReportResult.Canceled => IpcBuildLogCompletionReason.Canceled,
            _ => IpcBuildLogCompletionReason.Failed,
        };
    }
}
