namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines the closed <c>verify</c> stream event set. </summary>
public static class VerifyProgressEventNames
{
    /// <summary> Gets the event emitted when verify profile execution starts. </summary>
    public const string Started = "verify.started";

    /// <summary> Gets the event emitted when one verify profile step starts. </summary>
    public const string StepStarted = "verify.step.started";

    /// <summary> Gets the event emitted when one verify profile step completes. </summary>
    public const string StepCompleted = "verify.step.completed";

    /// <summary> Gets the event emitted when one conditional verify profile step is skipped. </summary>
    public const string StepSkipped = "verify.step.skipped";

    /// <summary> Gets the event emitted for structured non-terminal verify diagnostics. </summary>
    public const string Diagnostic = "verify.diagnostic";

    /// <summary> Gets the event emitted when verify profile execution completes with a final verdict. </summary>
    public const string Completed = "verify.completed";
}
