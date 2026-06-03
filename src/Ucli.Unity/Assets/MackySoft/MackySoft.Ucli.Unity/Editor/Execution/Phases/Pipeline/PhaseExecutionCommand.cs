namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents the top-level command executed by the phase executor. </summary>
    internal enum PhaseExecutionCommand
    {
        /// <summary> Runs <c>validate -&gt; plan</c> phases. </summary>
        Plan = 0,

        /// <summary> Runs <c>validate -&gt; plan -&gt; call</c> phases. </summary>
        Call = 1,

        /// <summary> Runs <c>validate -&gt; plan</c> phases without issuing a plan token. </summary>
        PlanWithoutToken = 2,
    }
}
