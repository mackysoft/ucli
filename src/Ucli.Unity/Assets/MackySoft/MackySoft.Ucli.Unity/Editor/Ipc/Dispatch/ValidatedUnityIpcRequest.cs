using System;
using System.Text.Json;
using MackySoft.Text.Vocabularies;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Carries one authorized Unity IPC request after protocol and literal validation. </summary>
    internal sealed class ValidatedUnityIpcRequest : IIpcRequestCorrelation
    {
        /// <summary> Initializes one validated Unity IPC request. </summary>
        /// <param name="requestId"> The non-empty request identifier. </param>
        /// <param name="method"> The defined Unity IPC method. </param>
        /// <param name="payload"> The method payload. </param>
        /// <param name="responseMode"> The defined response framing mode. </param>
        /// <param name="requestDeadlineUtc"> The UTC deadline shared by every delivery attempt for the logical request. </param>
        /// <param name="requestDeadlineRemainingMilliseconds"> The positive monotonic-clock time remaining when this delivery attempt started. </param>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> is empty or the deadline is not a UTC timestamp. </exception>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when a contract literal is undefined or the remaining deadline is not positive. </exception>
        public ValidatedUnityIpcRequest (
            Guid requestId,
            UnityIpcMethod method,
            JsonElement payload,
            IpcResponseMode responseMode,
            DateTimeOffset requestDeadlineUtc,
            int requestDeadlineRemainingMilliseconds)
        {
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("Request id must not be empty.", nameof(requestId));
            }

            if (!TextVocabulary.IsDefined(method))
            {
                throw new ArgumentOutOfRangeException(nameof(method), method, "Unity IPC method must be defined.");
            }

            if (!TextVocabulary.IsDefined(responseMode))
            {
                throw new ArgumentOutOfRangeException(nameof(responseMode), responseMode, "IPC response mode must be defined.");
            }

            if (requestDeadlineRemainingMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requestDeadlineRemainingMilliseconds),
                    requestDeadlineRemainingMilliseconds,
                    "Request deadline remaining milliseconds must be greater than zero.");
            }

            RequestId = requestId;
            Method = method;
            Payload = payload;
            ResponseMode = responseMode;
            RequestDeadlineUtc = ContractArgumentGuard.RequireUtcTimestamp(
                requestDeadlineUtc,
                nameof(requestDeadlineUtc));
            RequestDeadlineRemainingMilliseconds = requestDeadlineRemainingMilliseconds;
        }

        /// <inheritdoc />
        public Guid RequestId { get; }

        /// <summary> Gets the defined Unity IPC method. </summary>
        public UnityIpcMethod Method { get; }

        /// <summary> Gets the method-specific request payload. </summary>
        public JsonElement Payload { get; }

        /// <summary> Gets the defined response framing mode. </summary>
        public IpcResponseMode ResponseMode { get; }

        /// <summary> Gets the UTC deadline shared by every delivery attempt for the logical request. </summary>
        public DateTimeOffset RequestDeadlineUtc { get; }

        /// <summary> Gets the positive monotonic-clock time remaining when this delivery attempt started. </summary>
        public int RequestDeadlineRemainingMilliseconds { get; }
    }
}
