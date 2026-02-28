using System;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Validation;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Reads and validates execute-request header properties. </summary>
    internal static class ExecuteRequestHeaderReader
    {
        /// <summary> Reads one required protocol version. </summary>
        public static bool TryReadProtocolVersion (
            JsonElement requestArguments,
            out int protocolVersion,
            out ExecuteRequestNormalizationError? error)
        {
            protocolVersion = default;
            error = null;

            if (!requestArguments.TryGetProperty("protocolVersion", out var protocolVersionElement))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument("Request property 'protocolVersion' is required.", null);
                return false;
            }

            if (!protocolVersionElement.TryGetInt32(out protocolVersion))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument("Request property 'protocolVersion' must be an integer.", null);
                return false;
            }

            if (protocolVersion != IpcProtocol.CurrentVersion)
            {
                error = ExecuteRequestNormalizationError.ProtocolVersionMismatch(
                    expectedVersion: IpcProtocol.CurrentVersion,
                    actualVersion: protocolVersion);
                return false;
            }

            return true;
        }

        /// <summary> Reads one required request identifier. </summary>
        public static bool TryReadRequestId (
            JsonElement requestArguments,
            out string requestId,
            out ExecuteRequestNormalizationError? error)
        {
            requestId = string.Empty;
            error = null;

            if (!JsonStringContractReader.TryRead(
                jsonObject: requestArguments,
                propertyName: "requestId",
                presenceRequirement: JsonStringPresenceRequirement.Required,
                rejectEmptyOrWhitespace: true,
                rejectOuterWhitespace: true,
                value: out var rawRequestId,
                error: out var readError))
            {
                error = ExecuteRequestNormalizationErrorFactory.RequestId(readError);
                return false;
            }

            if (!Guid.TryParseExact(rawRequestId!, "D", out var parsedRequestId))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument("Request property 'requestId' must be UUID format 'D'.", null);
                return false;
            }

            requestId = parsedRequestId.ToString("D");
            return true;
        }
    }
}
