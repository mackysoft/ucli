using System;

#nullable enable

using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Represents one structured normalization error. </summary>
    /// <param name="Code"> The machine-readable error code. </param>
    /// <param name="Message"> The user-facing error message. </param>
    /// <param name="OpId"> The related operation identifier when available; otherwise <see langword="null" />. </param>
    internal sealed record ExecuteRequestNormalizationError
    {
        internal ExecuteRequestNormalizationError (
            UcliCode Code,
            string Message,
            IpcExecuteStepId? OpId)
        {
            this.Code = Code ?? throw new ArgumentNullException(nameof(Code));
            this.Message = Message;
            this.OpId = OpId;
        }

        public UcliCode Code { get; }

        public string Message { get; }

        public IpcExecuteStepId? OpId { get; }

        /// <summary> Creates one invalid argument error. </summary>
        /// <param name="message"> The user-facing error message. </param>
        /// <param name="opId"> The related operation identifier when available; otherwise <see langword="null" />. </param>
        /// <returns> One normalization error with <see cref="UcliCoreErrorCodes.InvalidArgument" /> code. </returns>
        internal static ExecuteRequestNormalizationError InvalidArgument (
            string message,
            IpcExecuteStepId? opId)
        {
            return new ExecuteRequestNormalizationError(
                Code: UcliCoreErrorCodes.InvalidArgument,
                Message: message,
                OpId: opId);
        }

        /// <summary> Creates one protocol version mismatch error. </summary>
        /// <param name="expectedVersion"> The supported protocol version. </param>
        /// <param name="actualVersion"> The received protocol version. </param>
        /// <returns> One normalization error with <see cref="IpcProtocolErrorCodes.ProtocolVersionMismatch" /> code. </returns>
        internal static ExecuteRequestNormalizationError ProtocolVersionMismatch (
            int expectedVersion,
            int actualVersion)
        {
            return new ExecuteRequestNormalizationError(
                Code: IpcProtocolErrorCodes.ProtocolVersionMismatch,
                Message: $"Protocol version mismatch. Expected {expectedVersion}, actual {actualVersion}.",
                OpId: null);
        }
    }
}
