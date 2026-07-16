#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Defines which persistence observations one normalized operation contributes to public execution results. </summary>
    internal enum OperationPersistenceReportingPolicy : byte
    {
        /// <summary> Reports every persistence observation produced by the operation. </summary>
        ReportAll = 1,

        /// <summary> Suppresses touched resources, read invalidations, and persisted observations. </summary>
        SuppressAll = 2,

        /// <summary> Suppresses scene touched resources and scene read invalidations while preserving other observations. </summary>
        SuppressScene = 3,
    }
}
