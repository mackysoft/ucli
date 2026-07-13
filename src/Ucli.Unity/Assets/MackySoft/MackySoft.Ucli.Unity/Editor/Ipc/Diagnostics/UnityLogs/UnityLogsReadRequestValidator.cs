using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Validates Unity-log read payload and resolves normalized filter values. </summary>
    internal sealed class UnityLogsReadRequestValidator
    {
        /// <summary> Tries to validate one Unity-log read payload. </summary>
        /// <param name="request"> The request payload. </param>
        /// <param name="currentStreamId"> The current stream identifier. </param>
        /// <param name="filter"> The normalized read filter. </param>
        /// <param name="errorMessage"> The invalid-argument error message when validation fails. </param>
        /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
        public bool TryValidate (
            IpcUnityLogsReadRequest request,
            Guid currentStreamId,
            out UnityLogsReadFilter filter,
            out string errorMessage)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (currentStreamId == Guid.Empty)
            {
                throw new ArgumentException("Current stream id must not be empty.", nameof(currentStreamId));
            }

            if (!TryResolveAfterSequence(request.After, currentStreamId, out var afterSequence, out errorMessage))
            {
                filter = null;
                return false;
            }

            if (!IpcUnityLogsReadRequestNormalizer.TryNormalize(
                    request,
                    out var normalizedRequest,
                    out var since,
                    out var until,
                    out errorMessage))
            {
                filter = null;
                return false;
            }

            filter = new UnityLogsReadFilter(
                AfterSequence: afterSequence,
                Tail: normalizedRequest!.Tail,
                Since: since,
                Until: until,
                Level: normalizedRequest.Level!,
                Query: normalizedRequest.Query,
                QueryTarget: normalizedRequest.QueryTarget!,
                Source: normalizedRequest.Source!,
                StackTraceMode: normalizedRequest.StackTrace!,
                StackTraceMaxFrames: normalizedRequest.StackTraceMaxFrames,
                StackTraceMaxChars: normalizedRequest.StackTraceMaxChars);
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryResolveAfterSequence (
            string afterCursor,
            Guid currentStreamId,
            out long? afterSequence,
            out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(afterCursor))
            {
                afterSequence = null;
                errorMessage = string.Empty;
                return true;
            }

            if (!IpcLogCursorCodec.TryParse(afterCursor, out var parsedStreamId, out var parsedSequence))
            {
                afterSequence = null;
                errorMessage = $"after is invalid cursor format. Actual: {afterCursor}.";
                return false;
            }

            if (parsedStreamId != currentStreamId)
            {
                afterSequence = null;
                errorMessage = $"after streamId does not match current unity log stream. actual={parsedStreamId}, current={currentStreamId}.";
                return false;
            }

            afterSequence = parsedSequence;
            errorMessage = string.Empty;
            return true;
        }

    }
}
