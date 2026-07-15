using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Transport;

/// <summary> Reads protocol-level failure signals from IPC response envelopes. </summary>
internal static class IpcResponseFailureReader
{
    /// <summary> Tries to read one response failure from status and error entries. </summary>
    /// <param name="response"> The IPC response envelope. </param>
    /// <param name="firstError"> The first response error when present; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when response indicates failure; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="response" /> is <see langword="null" />. </exception>
    public static bool TryRead (
        IpcResponse response,
        [NotNullWhen(true)] out IpcError? firstError)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.Status == IpcResponseStatus.Ok)
        {
            firstError = null;
            return false;
        }

        firstError = response.Errors[0];
        return true;
    }
}
