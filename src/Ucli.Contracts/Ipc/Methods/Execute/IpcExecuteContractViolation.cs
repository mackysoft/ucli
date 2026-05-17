namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one runtime violation of an operation's declared execution contract. </summary>
/// <param name="OpId"> The public step identifier associated with the violation. </param>
/// <param name="Operation"> The public operation name associated with the violation. </param>
/// <param name="ExpectedFact"> The declared contract fact that should have held. </param>
/// <param name="ObservedResult"> The observed execution result that violated the fact. </param>
/// <param name="ApplicationState"> The best-known application state after observing the violation. </param>
public sealed record IpcExecuteContractViolation (
    string OpId,
    string Operation,
    string ExpectedFact,
    string ObservedResult,
    string ApplicationState);
