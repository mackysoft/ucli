namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one operation phase state in execution traces. </summary>
    internal enum OperationPhase
    {
        /// <summary> The operation completed the validate phase. </summary>
        Validate = 0,

        /// <summary> The operation completed the plan phase. </summary>
        Plan = 1,

        /// <summary> The operation completed the call phase. </summary>
        Call = 2,

        /// <summary> The operation was skipped due to a prior failure. </summary>
        Skipped = 3,
    }
}
