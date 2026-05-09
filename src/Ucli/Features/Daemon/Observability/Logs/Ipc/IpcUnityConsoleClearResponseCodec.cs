using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

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

        if (IpcResponseFailureReader.TryRead(response, out var firstError, out var status))
        {
            if (firstError is not null)
            {
                error = firstError.Code == UcliCoreErrorCodes.InvalidArgument
                    ? ExecutionError.InvalidArgument($"Unity Console clear failed with error code '{firstError.Code}'. {firstError.Message}")
                    : ExecutionError.InternalError($"Unity Console clear failed with error code '{firstError.Code}'. {firstError.Message}");
                return false;
            }

            error = ExecutionError.InternalError($"Unity Console clear failed with status '{status}'.");
            return false;
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcUnityConsoleClearResponse _, out var readError))
        {
            error = ExecutionError.InternalError($"Unity Console clear payload is invalid. {readError.Message}");
            return false;
        }

        error = null;
        return true;
    }
}
