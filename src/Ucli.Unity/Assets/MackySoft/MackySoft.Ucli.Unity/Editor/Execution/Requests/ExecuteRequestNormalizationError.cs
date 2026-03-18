#nullable enable

using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Represents one structured normalization error. </summary>
    /// <param name="Code"> The machine-readable error code. </param>
    /// <param name="Message"> The user-facing error message. </param>
    /// <param name="OpId"> The related operation identifier when available; otherwise <see langword="null" />. </param>
    internal sealed record ExecuteRequestNormalizationError (
        string Code,
        string Message,
        string? OpId)
    {
        /// <summary> Creates one invalid argument error. </summary>
        /// <param name="message"> The user-facing error message. </param>
        /// <param name="opId"> The related operation identifier when available; otherwise <see langword="null" />. </param>
        /// <returns> One normalization error with <see cref="IpcErrorCodes.InvalidArgument" /> code. </returns>
        internal static ExecuteRequestNormalizationError InvalidArgument (
            string message,
            string? opId)
        {
            return new ExecuteRequestNormalizationError(
                Code: IpcErrorCodes.InvalidArgument,
                Message: message,
                OpId: opId);
        }

        /// <summary> Creates one protocol version mismatch error. </summary>
        /// <param name="expectedVersion"> The supported protocol version. </param>
        /// <param name="actualVersion"> The received protocol version. </param>
        /// <returns> One normalization error with <see cref="IpcErrorCodes.ProtocolVersionMismatch" /> code. </returns>
        internal static ExecuteRequestNormalizationError ProtocolVersionMismatch (
            int expectedVersion,
            int actualVersion)
        {
            return new ExecuteRequestNormalizationError(
                Code: IpcErrorCodes.ProtocolVersionMismatch,
                Message: $"Protocol version mismatch. Expected {expectedVersion}, actual {actualVersion}.",
                OpId: null);
        }
    }
}