using System;

#nullable enable

using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Represents one structured normalization error. </summary>
    /// <param name="Code"> The machine-readable error code. </param>
    /// <param name="Message"> The user-facing error message. </param>
    /// <param name="InstancePath"> The RFC 6901 path of the related request value when available; otherwise <see langword="null" />. </param>
    internal sealed record ExecuteRequestNormalizationError
    {
        internal ExecuteRequestNormalizationError (
            UcliCode Code,
            string Message,
            string? InstancePath)
        {
            this.Code = Code ?? throw new ArgumentNullException(nameof(Code));
            this.Message = Message;
            this.InstancePath = InstancePath;
        }

        public UcliCode Code { get; }

        public string Message { get; }

        public string? InstancePath { get; }

        /// <summary> Creates one invalid argument error. </summary>
        /// <param name="message"> The user-facing error message. </param>
        /// <param name="instancePath"> The RFC 6901 path of the related request value when available; otherwise <see langword="null" />. </param>
        /// <returns> One normalization error with <see cref="UcliCoreErrorCodes.InvalidArgument" /> code. </returns>
        internal static ExecuteRequestNormalizationError InvalidArgument (
            string message,
            string? instancePath)
        {
            return new ExecuteRequestNormalizationError(
                Code: UcliCoreErrorCodes.InvalidArgument,
                Message: message,
                InstancePath: instancePath);
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
                InstancePath: "/protocolVersion");
        }
    }
}
