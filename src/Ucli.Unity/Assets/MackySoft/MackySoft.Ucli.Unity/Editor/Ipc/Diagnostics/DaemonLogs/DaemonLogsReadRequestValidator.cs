using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Validates daemon-log read payload and resolves normalized filter values. </summary>
    internal sealed class DaemonLogsReadRequestValidator : IDaemonLogsReadRequestValidator
    {
        /// <inheritdoc />
        public bool TryValidate (
            IpcDaemonLogsReadRequest request,
            Guid currentStreamId,
            out DaemonLogsReadFilter filter,
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

            if (!IpcDaemonLogsReadRequestNormalizer.TryNormalize(
                    request,
                    out var normalizedRequest,
                    out var since,
                    out var until,
                    out errorMessage))
            {
                filter = null;
                return false;
            }

            filter = new DaemonLogsReadFilter(
                AfterSequence: afterSequence,
                Tail: normalizedRequest!.Tail,
                Since: since,
                Until: until,
                Level: normalizedRequest.Level!,
                Query: normalizedRequest.Query,
                QueryTarget: normalizedRequest.QueryTarget!,
                Category: normalizedRequest.Category);
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
                errorMessage = $"after streamId does not match current daemon stream. actual={parsedStreamId}, current={currentStreamId}.";
                return false;
            }

            afterSequence = parsedSequence;
            errorMessage = string.Empty;
            return true;
        }
    }
}
