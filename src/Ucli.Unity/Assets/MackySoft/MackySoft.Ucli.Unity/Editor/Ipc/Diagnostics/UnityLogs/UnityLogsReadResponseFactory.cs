using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Builds Unity-log read response payload from filtered events. </summary>
    internal sealed class UnityLogsReadResponseFactory
    {
        /// <summary> Creates Unity-log read response payload. </summary>
        /// <param name="events"> The filtered Unity-log events. </param>
        /// <param name="nextCursor"> The next stream cursor value. </param>
        /// <returns> The Unity-log read response payload. </returns>
        public IpcUnityLogsReadResponse Create (
            IReadOnlyList<UnityLogsReadEvent> events,
            IpcLogCursor nextCursor)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            if (nextCursor == null)
            {
                throw new ArgumentNullException(nameof(nextCursor));
            }

            var contractEvents = new IpcUnityLogEvent[events.Count];
            for (var i = 0; i < events.Count; i++)
            {
                var unityLogEvent = events[i];
                contractEvents[i] = new IpcUnityLogEvent(
                    Timestamp: unityLogEvent.Timestamp,
                    Level: unityLogEvent.Level,
                    Source: unityLogEvent.Source,
                    Message: unityLogEvent.Message,
                    StackTrace: unityLogEvent.StackTrace,
                    Cursor: unityLogEvent.Cursor);
            }

            return new IpcUnityLogsReadResponse(
                Events: contractEvents,
                NextCursor: nextCursor);
        }
    }
}
