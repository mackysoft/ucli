namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines the closed <c>compile</c> stream event set. </summary>
public static class CompileProgressEventNames
{
    /// <summary> Gets the event emitted after compile run identity and execution target are established. </summary>
    public const string Started = "compile.started";

    /// <summary> Gets the event emitted when the host starts dispatching the compile refresh request. </summary>
    public const string RefreshStarted = "compile.refresh.started";

    /// <summary> Gets the event emitted when a completed summary is recovered from artifacts. </summary>
    public const string Recovered = "compile.recovered";

    /// <summary> Gets the event emitted when startup diagnosis is projected into a diagnostics-read summary. </summary>
    public const string Diagnostic = "compile.diagnostic";

    /// <summary> Gets the event emitted after the final compile payload has been built. </summary>
    public const string Completed = "compile.completed";
}
