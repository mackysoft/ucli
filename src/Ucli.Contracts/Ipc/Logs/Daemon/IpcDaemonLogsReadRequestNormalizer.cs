using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Validates and normalizes common filter values used by <c>daemon.logs.read</c>. </summary>
public static class IpcDaemonLogsReadRequestNormalizer
{
    /// <summary> Tries to normalize one daemon-log read request payload. </summary>
    public static bool TryNormalize (
        IpcDaemonLogsReadRequest request,
        out IpcDaemonLogsReadRequest? normalizedRequest,
        out DateTimeOffset? sinceTimestamp,
        out DateTimeOffset? untilTimestamp,
        out string? errorMessage)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        normalizedRequest = null;
        if (!IpcLogsReadWindowNormalizer.TryNormalize(
                request.Tail,
                request.Since,
                request.Until,
                out sinceTimestamp,
                out untilTimestamp,
                out errorMessage))
        {
            return false;
        }

        var queryTarget = request.QueryTarget ?? IpcLogQueryTarget.Message;
        if (queryTarget == IpcLogQueryTarget.Stack)
        {
            errorMessage =
                $"queryTarget '{ContractLiteralCodec.ToValue(IpcLogQueryTarget.Stack)}' is not supported for daemon logs. "
                + $"Supported: {ContractLiteralCodec.ToValue(IpcLogQueryTarget.Message)}, {ContractLiteralCodec.ToValue(IpcLogQueryTarget.Both)}.";
            return false;
        }

        normalizedRequest = new IpcDaemonLogsReadRequest(
            Tail: request.Tail,
            After: request.After,
            Since: request.Since,
            Until: request.Until,
            Level: request.Level,
            Query: StringValueNormalizer.TrimToNull(request.Query),
            QueryTarget: queryTarget,
            Category: StringValueNormalizer.TrimToNull(request.Category));
        errorMessage = null;
        return true;
    }
}
