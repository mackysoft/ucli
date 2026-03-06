using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Builds daemon-log read response payload from filtered events. </summary>
    internal sealed class DaemonLogsReadResponseFactory
    {
        /// <summary> Creates daemon-log read response payload. </summary>
        /// <param name="events"> The filtered daemon-log events. </param>
        /// <param name="nextCursor"> The next stream cursor value. </param>
        /// <returns> The daemon-log read response payload. </returns>
        public IpcDaemonLogsReadResponse Create (
            IReadOnlyList<DaemonLogEvent> events,
            string nextCursor)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            if (nextCursor == null)
            {
                throw new ArgumentNullException(nameof(nextCursor));
            }

            var contractEvents = new IpcDaemonLogEvent[events.Count];
            for (var i = 0; i < events.Count; i++)
            {
                var daemonLogEvent = events[i];
                contractEvents[i] = new IpcDaemonLogEvent(
                    Timestamp: daemonLogEvent.Timestamp,
                    Level: daemonLogEvent.Level,
                    Category: daemonLogEvent.Category,
                    Message: daemonLogEvent.Message,
                    Raw: daemonLogEvent.Raw,
                    Cursor: daemonLogEvent.Cursor);
            }

            return new IpcDaemonLogsReadResponse(
                Events: contractEvents,
                NextCursor: nextCursor);
        }
    }
}
