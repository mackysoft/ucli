using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Transport;

/// <summary> Reads protocol-level failure signals from IPC response envelopes. </summary>
internal static class IpcResponseFailureReader
{
    /// <summary> Tries to read one response failure from status and error entries. </summary>
    /// <param name="response"> The IPC response envelope. </param>
    /// <param name="firstError"> The first response error when present; otherwise <see langword="null" />. </param>
    /// <param name="status"> The non-success status when error entries are absent; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when response indicates failure; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="response" /> is <see langword="null" />. </exception>
    public static bool TryRead (
        IpcResponse response,
        out IpcError? firstError,
        out string? status)
    {
        ArgumentNullException.ThrowIfNull(response);

        var hasSuccessStatus = string.Equals(response.Status, IpcProtocol.StatusOk, StringComparison.Ordinal);
        var hasErrorEntries = response.Errors.Count > 0;
        if (hasSuccessStatus && !hasErrorEntries)
        {
            firstError = null;
            status = null;
            return false;
        }

        if (hasErrorEntries)
        {
            firstError = response.Errors[0];
            status = null;
            return true;
        }

        firstError = null;
        status = response.Status;
        return true;
    }
}