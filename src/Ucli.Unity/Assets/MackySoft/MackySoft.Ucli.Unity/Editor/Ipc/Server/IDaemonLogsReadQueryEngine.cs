using System.Collections.Generic;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Applies daemon-log read filters to one snapshot event sequence. </summary>
    internal interface IDaemonLogsReadQueryEngine
    {
        /// <summary> Filters daemon-log events by normalized filter values. </summary>
        /// <param name="events"> The source daemon-log events. </param>
        /// <param name="filter"> The normalized filter predicates. </param>
        /// <returns> The filtered daemon-log events. </returns>
        IReadOnlyList<DaemonLogEvent> Filter (
            IReadOnlyList<DaemonLogEvent> events,
            DaemonLogsReadFilter filter);
    }
}
