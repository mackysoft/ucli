using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>daemon.logs.read</c> IPC method requests. </summary>
    internal sealed class DaemonLogsReadUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private const string QueryTargetMessage = "message";

        private const string QueryTargetBoth = "both";

        private const string QueryTargetStack = "stack";

        private readonly IDaemonLogStream daemonLogStream;

        private readonly IDaemonLogger daemonLogger;

        /// <summary> Initializes a new instance of the <see cref="DaemonLogsReadUnityIpcMethodHandler" /> class. </summary>
        /// <param name="daemonLogStream"> The daemon-log stream dependency. </param>
        /// <param name="daemonLogger"> The daemon daemon-logger dependency. </param>
        public DaemonLogsReadUnityIpcMethodHandler (
            IDaemonLogStream daemonLogStream,
            IDaemonLogger daemonLogger = null)
        {
            this.daemonLogStream = daemonLogStream ?? throw new ArgumentNullException(nameof(daemonLogStream));
            this.daemonLogger = daemonLogger ?? NoOpDaemonLogger.Instance;
        }

        /// <inheritdoc />
        public string Method => IpcMethodNames.DaemonLogsRead;

        /// <inheritdoc />
        public ValueTask<IpcResponse> Handle (
            IpcRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodeDaemonLogsReadRequest(
                    request,
                    out IpcDaemonLogsReadRequest payload,
                    out var decodeErrorResponse))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    "Daemon logs read payload decode failed.",
                    decodeErrorResponse?.Errors.Count > 0 ? decodeErrorResponse.Errors[0].Message : null);
                return new ValueTask<IpcResponse>(decodeErrorResponse);
            }

            var snapshot = daemonLogStream.Snapshot();

            if (!TryResolveAfterSequence(payload.After, snapshot.StreamId, out var afterSequence, out var afterErrorMessage))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    "Daemon logs read rejected due to invalid after cursor.",
                    afterErrorMessage);
                return new ValueTask<IpcResponse>(CreateInvalidArgumentResponse(request, afterErrorMessage));
            }

            if (!TryResolveTail(payload.Tail, out var tail, out var tailErrorMessage))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    "Daemon logs read rejected due to invalid tail value.",
                    tailErrorMessage);
                return new ValueTask<IpcResponse>(CreateInvalidArgumentResponse(request, tailErrorMessage));
            }

            if (!TryResolveTimeRange(payload.Since, payload.Until, out var since, out var until, out var timeRangeErrorMessage))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    "Daemon logs read rejected due to invalid time range.",
                    timeRangeErrorMessage);
                return new ValueTask<IpcResponse>(CreateInvalidArgumentResponse(request, timeRangeErrorMessage));
            }

            if (!TryResolveLevel(payload.Level, out var level, out var levelErrorMessage))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    "Daemon logs read rejected due to invalid level.",
                    levelErrorMessage);
                return new ValueTask<IpcResponse>(CreateInvalidArgumentResponse(request, levelErrorMessage));
            }

            if (!TryResolveQueryTarget(payload.QueryTarget, out var queryTarget, out var queryTargetErrorMessage))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    "Daemon logs read rejected due to invalid queryTarget.",
                    queryTargetErrorMessage);
                return new ValueTask<IpcResponse>(CreateInvalidArgumentResponse(request, queryTargetErrorMessage));
            }

            var filteredEvents = FilterEvents(
                snapshot.Events,
                afterSequence,
                since,
                until,
                level,
                payload.Query,
                queryTarget,
                payload.Category,
                tail);
            var contractEvents = new IpcDaemonLogEvent[filteredEvents.Count];
            for (var i = 0; i < filteredEvents.Count; i++)
            {
                var daemonLogEvent = filteredEvents[i];
                contractEvents[i] = new IpcDaemonLogEvent(
                    Timestamp: daemonLogEvent.Timestamp,
                    Level: daemonLogEvent.Level,
                    Category: daemonLogEvent.Category,
                    Message: daemonLogEvent.Message,
                    Raw: daemonLogEvent.Raw,
                    Cursor: daemonLogEvent.Cursor);
            }

            var responsePayload = new IpcDaemonLogsReadResponse(
                Events: contractEvents,
                NextCursor: snapshot.NextCursor);
            return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateSuccessResponse(request, responsePayload));
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
                level = "all";
                errorMessage = string.Empty;
                return true;
            }

            var normalizedValue = value.Trim().ToLowerInvariant();
            if (string.Equals(normalizedValue, "all", StringComparison.Ordinal)
                || string.Equals(normalizedValue, DaemonLogLevels.Error, StringComparison.Ordinal)
                || string.Equals(normalizedValue, DaemonLogLevels.Warning, StringComparison.Ordinal)
                || string.Equals(normalizedValue, DaemonLogLevels.Info, StringComparison.Ordinal))
            {
                level = normalizedValue;
                errorMessage = string.Empty;
                return true;
            }

            level = string.Empty;
            errorMessage = $"level must be one of: all, error, warning, info. Actual: {value}.";
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
                queryTarget = QueryTargetMessage;
                errorMessage = string.Empty;
                return true;
            }

            var normalizedValue = value.Trim().ToLowerInvariant();
            if (string.Equals(normalizedValue, QueryTargetStack, StringComparison.Ordinal))
            {
                queryTarget = string.Empty;
                errorMessage = "queryTarget 'stack' is not supported for daemon logs. Supported: message, both.";
                return false;
            }

            if (string.Equals(normalizedValue, QueryTargetMessage, StringComparison.Ordinal)
                || string.Equals(normalizedValue, QueryTargetBoth, StringComparison.Ordinal))
            {
                queryTarget = normalizedValue;
                errorMessage = string.Empty;
                return true;
            }

            queryTarget = string.Empty;
            errorMessage = $"queryTarget must be one of: message, both. Actual: {value}.";
            return false;
        }

        /// <summary> Filters daemon log events by request-specified predicates. </summary>
        /// <param name="events"> The source event sequence. </param>
        /// <param name="afterSequence"> The optional lower cursor sequence bound. </param>
        /// <param name="since"> The optional lower time bound. </param>
        /// <param name="until"> The optional upper time bound. </param>
        /// <param name="level"> The normalized level filter literal. </param>
        /// <param name="query"> The optional free-text query value. </param>
        /// <param name="queryTarget"> The normalized query-target literal. </param>
        /// <param name="category"> The optional category filter value. </param>
        /// <param name="tail"> The optional tail count value. </param>
        /// <returns> The filtered daemon log event list. </returns>
        private static List<DaemonLogEvent> FilterEvents (
            IReadOnlyList<DaemonLogEvent> events,
            long? afterSequence,
            DateTimeOffset? since,
            DateTimeOffset? until,
            string level,
            string query,
            string queryTarget,
            string category,
            int? tail)
        {
            var filteredEvents = new List<DaemonLogEvent>(events.Count);
            foreach (var daemonLogEvent in events)
            {
                if (afterSequence.HasValue && daemonLogEvent.Sequence < afterSequence.Value)
                {
                    continue;
                }

                if (since.HasValue && TryParseEventTimestamp(daemonLogEvent.Timestamp, out var eventTimestampSince) && eventTimestampSince < since.Value)
                {
                    continue;
                }

                if (until.HasValue && TryParseEventTimestamp(daemonLogEvent.Timestamp, out var eventTimestampUntil) && eventTimestampUntil > until.Value)
                {
                    continue;
                }

                if (!string.Equals(level, "all", StringComparison.Ordinal)
                    && !string.Equals(daemonLogEvent.Level, level, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(category)
                    && !string.Equals(daemonLogEvent.Category, category.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(query)
                    && !MatchesQuery(daemonLogEvent, query, queryTarget))
                {
                    continue;
                }

                filteredEvents.Add(daemonLogEvent);
            }

            if (!tail.HasValue || filteredEvents.Count <= tail.Value)
            {
                return filteredEvents;
            }

            var tailEvents = new List<DaemonLogEvent>(tail.Value);
            var startIndex = filteredEvents.Count - tail.Value;
            for (var i = startIndex; i < filteredEvents.Count; i++)
            {
                tailEvents.Add(filteredEvents[i]);
            }

            return tailEvents;
        }

        /// <summary> Determines whether one daemon log event matches query filter. </summary>
        /// <param name="daemonLogEvent"> The daemon log event value. </param>
        /// <param name="query"> The normalized query value. </param>
        /// <param name="queryTarget"> The normalized query-target literal. </param>
        /// <returns> <see langword="true" /> when event matches query; otherwise <see langword="false" />. </returns>
        private static bool MatchesQuery (
            DaemonLogEvent daemonLogEvent,
            string query,
            string queryTarget)
        {
            var normalizedQuery = query.Trim();
            var messageHit = daemonLogEvent.Message != null
                && daemonLogEvent.Message.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase) >= 0;
            if (string.Equals(queryTarget, QueryTargetMessage, StringComparison.Ordinal))
            {
                return messageHit;
            }

            var rawHit = daemonLogEvent.Raw != null
                && daemonLogEvent.Raw.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase) >= 0;
            return messageHit || rawHit;
        }

        /// <summary> Tries to parse one daemon-log event timestamp string. </summary>
        /// <param name="timestampText"> The source timestamp text. </param>
        /// <param name="timestamp"> The parsed timestamp when successful. </param>
        /// <returns> <see langword="true" /> when parsing succeeded; otherwise <see langword="false" />. </returns>
        private static bool TryParseEventTimestamp (
            string timestampText,
            out DateTimeOffset timestamp)
        {
            return DateTimeOffset.TryParse(
                timestampText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out timestamp);
        }

        /// <summary> Creates one invalid-argument response envelope. </summary>
        /// <param name="request"> The request envelope context. </param>
        /// <param name="message"> The user-facing error message. </param>
        /// <returns> The invalid-argument response envelope. </returns>
        private static IpcResponse CreateInvalidArgumentResponse (
            IpcRequest request,
            string message)
        {
            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                IpcErrorCodes.InvalidArgument,
                message,
                null);
        }
    }
}
