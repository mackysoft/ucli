using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;

/// <summary> Decodes Unity Editor Console clear IPC response envelopes into validated payload values. </summary>
internal static class IpcUnityConsoleClearResponseCodec
{
    /// <summary> Tries to decode one Unity Editor Console clear IPC response. </summary>
    /// <param name="response"> The IPC response envelope. </param>
    /// <param name="error"> The structured decode error when decode fails. </param>
    /// <returns> <see langword="true" /> when decode succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryDecode (
        IpcResponse response,
        out ExecutionError? error)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (IpcLogsResponseDecodeHelper.TryDecodeFailure(response, "Unity Console clear", out error))
        {
            return false;
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcUnityConsoleClearResponse _, out var readError))
        {
            error = IpcLogsResponseDecodeHelper.CreateInvalidPayloadError("Unity Console clear", readError.Message);
            return false;
        }

        error = null;
        return true;
    }
}
