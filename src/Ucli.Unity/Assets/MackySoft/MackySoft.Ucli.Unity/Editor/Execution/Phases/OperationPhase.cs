namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents the final phase reached by an operation in execution traces. </summary>
    internal enum OperationPhase
    {
        /// <summary> The operation reached the validate phase and ended there. </summary>
        Validate = 0,

        /// <summary> The operation reached the plan phase and ended there. </summary>
        Plan = 1,

        /// <summary> The operation reached the call phase and ended there. </summary>
        Call = 2,

        /// <summary> The operation was skipped due to a prior failure. </summary>
        Skipped = 3,
    }
}