namespace MackySoft.Ucli.Contracts.Testing;

/// <summary> Defines the closed <c>test.run</c> stream event set. </summary>
public static class TestRunProgressEventNames
{
    /// <summary> Gets the event emitted after run identity is established and before Unity execution starts. </summary>
    public const string RunStarted = "test.run.started";

    /// <summary> Gets the event emitted when one concrete test case starts. </summary>
    public const string CaseStarted = "test.case.started";

    /// <summary> Gets the event emitted when one concrete test case finishes. </summary>
    public const string CaseFinished = "test.case.finished";

    /// <summary> Gets the event emitted for structured non-terminal run diagnostics. </summary>
    public const string RunDiagnostic = "test.run.diagnostic";
}
