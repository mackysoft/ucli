namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Defines runtime contract violation application-state literals before IPC projection. </summary>
    internal static class OperationContractViolationApplicationStateNames
    {
        /// <summary> The operation reports that it applied work before the violation was detected. </summary>
        public const string Applied = "applied";

        /// <summary> The operation reports no applied work and no mutation evidence. </summary>
        public const string NotApplied = "notApplied";

        /// <summary> The operation reports mutation evidence without applied confirmation. </summary>
        public const string Indeterminate = "indeterminate";
    }
}
