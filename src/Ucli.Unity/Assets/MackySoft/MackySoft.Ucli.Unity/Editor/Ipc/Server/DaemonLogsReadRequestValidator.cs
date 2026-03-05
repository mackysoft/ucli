using System;
using System.Globalization;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Validates daemon-log read payload and resolves normalized filter values. </summary>
    internal sealed class DaemonLogsReadRequestValidator : IDaemonLogsReadRequestValidator
    {
        /// <inheritdoc />
        public bool TryValidate (
            IpcDaemonLogsReadRequest request,
            string currentStreamId,
            out DaemonLogsReadFilter filter,
            out string errorMessage)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(currentStreamId))
            {
                throw new ArgumentException("current stream id must not be null or empty.", nameof(currentStreamId));
            }

            if (!TryResolveAfterSequence(request.After, currentStreamId, out var afterSequence, out errorMessage))
            {
                filter = null;
                return false;
            }

            if (!TryResolveTail(request.Tail, out var tail, out errorMessage))
            {
                filter = null;
                return false;
            }

            if (!TryResolveTimeRange(request.Since, request.Until, out var since, out var until, out errorMessage))
            {
                filter = null;
                return false;
            }

            if (!TryResolveLevel(request.Level, out var level, out errorMessage))
            {
                filter = null;
                return false;
            }

            if (!TryResolveQueryTarget(request.QueryTarget, out var queryTarget, out errorMessage))
            {
                filter = null;
                return false;
            }

            filter = new DaemonLogsReadFilter(
                AfterSequence: afterSequence,
                Tail: tail,
                Since: since,
                Until: until,
                Level: level,
                Query: StringValueNormalizer.TrimToNull(request.Query),
                QueryTarget: queryTarget,
                Category: StringValueNormalizer.TrimToNull(request.Category));
            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Tries to resolve and validate after cursor value. </summary>
        /// <param name="afterCursor"> The optional after cursor value. </param>
        /// <param name="currentStreamId"> The current daemon stream identifier. </param>
        /// <param name="afterSequence"> The resolved after sequence value. </param>
        /// <param name="errorMessage"> The invalid-argument error message when validation fails. </param>
        /// <returns> <see langword="true" /> when value is valid; otherwise <see langword="false" />. </returns>
        private static bool TryResolveAfterSequence (
            string afterCursor,
            string currentStreamId,
            out long? afterSequence,
            out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(afterCursor))
            {
                afterSequence = null;
                errorMessage = string.Empty;
                return true;
            }

            if (!DaemonLogCursorCodec.TryParse(afterCursor, out var parsedStreamId, out var parsedSequence))
            {
                afterSequence = null;
                errorMessage = $"after is invalid cursor format. Actual: {afterCursor}.";
                return false;
            }

            if (!string.Equals(parsedStreamId, currentStreamId, StringComparison.Ordinal))
            {
                afterSequence = null;
                errorMessage = $"after streamId does not match current daemon stream. actual={parsedStreamId}, current={currentStreamId}.";
                return false;
            }

            afterSequence = parsedSequence;
            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Tries to resolve and validate tail option value. </summary>
        /// <param name="tail"> The optional tail value. </param>
        /// <param name="resolvedTail"> The resolved tail value. </param>
        /// <param name="errorMessage"> The invalid-argument error message when validation fails. </param>
        /// <returns> <see langword="true" /> when value is valid; otherwise <see langword="false" />. </returns>
        private static bool TryResolveTail (
            int? tail,
            out int? resolvedTail,
            out string errorMessage)
        {
            if (!tail.HasValue)
            {
                resolvedTail = null;
                errorMessage = string.Empty;
                return true;
            }

            if (tail.Value <= 0)
            {
                resolvedTail = null;
                errorMessage = $"tail must be greater than zero. Actual: {tail.Value}.";
                return false;
            }

            resolvedTail = tail;
            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Tries to resolve and validate time-range option values. </summary>
        /// <param name="sinceText"> The optional since text value. </param>
        /// <param name="untilText"> The optional until text value. </param>
        /// <param name="since"> The resolved since timestamp. </param>
        /// <param name="until"> The resolved until timestamp. </param>
        /// <param name="errorMessage"> The invalid-argument error message when validation fails. </param>
        /// <returns> <see langword="true" /> when values are valid; otherwise <see langword="false" />. </returns>
        private static bool TryResolveTimeRange (
            string sinceText,
            string untilText,
            out DateTimeOffset? since,
            out DateTimeOffset? until,
            out string errorMessage)
        {
            if (!TryParseIsoTimestamp(sinceText, out since))
            {
                until = null;
                errorMessage = $"since must be an ISO 8601 timestamp with timezone offset. Actual: {sinceText}.";
                return false;
            }

            if (!TryParseIsoTimestamp(untilText, out until))
            {
                errorMessage = $"until must be an ISO 8601 timestamp with timezone offset. Actual: {untilText}.";
                return false;
            }

            if (since.HasValue && until.HasValue && since.Value > until.Value)
            {
                errorMessage = $"since must be less than or equal to until. since={sinceText}, until={untilText}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Tries to parse one ISO 8601 timestamp with explicit timezone offset. </summary>
        /// <param name="value"> The source timestamp text. </param>
        /// <param name="timestamp"> The parsed timestamp when successful. </param>
        /// <returns> <see langword="true" /> when parsing succeeded; otherwise <see langword="false" />. </returns>
        private static bool TryParseIsoTimestamp (
            string value,
            out DateTimeOffset? timestamp)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                timestamp = null;
                return true;
            }

            if (!DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsedTimestamp))
            {
                timestamp = null;
                return false;
            }

            var normalizedValue = value.Trim();
            var hasOffset = normalizedValue.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
                || normalizedValue.Contains('+')
                || normalizedValue.LastIndexOf('-') > normalizedValue.IndexOf('T');
            if (!hasOffset)
            {
                timestamp = null;
                return false;
            }

            timestamp = parsedTimestamp;
            return true;
        }

        /// <summary> Tries to resolve one level filter literal. </summary>
        /// <param name="value"> The optional level literal value. </param>
        /// <param name="level"> The resolved level filter literal. </param>
        /// <param name="errorMessage"> The invalid-argument error message when validation fails. </param>
        /// <returns> <see langword="true" /> when value is valid; otherwise <see langword="false" />. </returns>
        private static bool TryResolveLevel (
            string value,
            out string level,
            out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                level = IpcDaemonLogsLevelCodec.All;
                errorMessage = string.Empty;
                return true;
            }

            if (IpcDaemonLogsLevelCodec.TryParse(value, out var normalizedValue))
            {
                level = normalizedValue!;
                errorMessage = string.Empty;
                return true;
            }

            level = string.Empty;
            errorMessage =
                $"level must be one of: {IpcDaemonLogsLevelCodec.All}, {IpcDaemonLogsLevelCodec.Error}, {IpcDaemonLogsLevelCodec.Warning}, {IpcDaemonLogsLevelCodec.Info}. Actual: {value}.";
            return false;
        }

        /// <summary> Tries to resolve one query-target filter literal. </summary>
        /// <param name="value"> The optional query-target literal value. </param>
        /// <param name="queryTarget"> The resolved query-target literal. </param>
        /// <param name="errorMessage"> The invalid-argument error message when validation fails. </param>
        /// <returns> <see langword="true" /> when value is valid; otherwise <see langword="false" />. </returns>
        private static bool TryResolveQueryTarget (
            string value,
            out string queryTarget,
            out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                queryTarget = IpcDaemonLogsQueryTargetCodec.Message;
                errorMessage = string.Empty;
                return true;
            }

            if (!IpcDaemonLogsQueryTargetCodec.TryParse(value, out var normalizedValue))
            {
                queryTarget = string.Empty;
                errorMessage = $"queryTarget must be one of: {IpcDaemonLogsQueryTargetCodec.Message}, {IpcDaemonLogsQueryTargetCodec.Both}. Actual: {value}.";
                return false;
            }

            if (string.Equals(normalizedValue, IpcDaemonLogsQueryTargetCodec.Stack, StringComparison.Ordinal))
            {
                queryTarget = string.Empty;
                errorMessage =
                    $"queryTarget '{IpcDaemonLogsQueryTargetCodec.Stack}' is not supported for daemon logs. Supported: {IpcDaemonLogsQueryTargetCodec.Message}, {IpcDaemonLogsQueryTargetCodec.Both}.";
                return false;
            }

            queryTarget = normalizedValue!;
            errorMessage = string.Empty;
            return true;
        }

    }
}
