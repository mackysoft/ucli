using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Resolves build log completion reason values from normalized BuildReport results. </summary>
public static class IpcBuildLogCompletionReasonResolver
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

    /// <summary> Maps one normalized BuildReport result literal to the corresponding build log completion reason. </summary>
    /// <param name="result"> The normalized BuildReport result literal. </param>
    /// <returns> The completion reason implied by <paramref name="result" />; <c>failed</c> when the result is unknown. </returns>
    public static IpcBuildLogCompletionReason FromReportResultLiteral (string result)
    {
        return ContractLiteralCodec.TryParse<IpcBuildReportResult>(result, out var parsedResult)
            ? FromReportResult(parsedResult)
            : IpcBuildLogCompletionReason.Failed;
    }
}
