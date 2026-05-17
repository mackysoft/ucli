namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one runtime violation of an operation's declared execution contract. </summary>
    /// <param name="OpId"> The operation identifier associated with the violation. </param>
    /// <param name="Operation"> The operation name associated with the violation. </param>
    /// <param name="ExpectedFact"> The declared contract fact that should have held. </param>
    /// <param name="ObservedResult"> The observed execution result that violated the fact. </param>
    /// <param name="ApplicationState"> The best-known application state after observing the violation. </param>
    internal sealed record OperationContractViolation (
        string OpId,
        string Operation,
        string ExpectedFact,
        string ObservedResult,
        string ApplicationState);
}
