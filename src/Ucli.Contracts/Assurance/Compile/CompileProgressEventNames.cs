namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines the closed <c>compile</c> stream event set. </summary>
public static class CompileProgressEventNames
{
    /// <summary> Gets the event emitted after compile execution identity and execution target are established. </summary>
    public const string Started = "compile.started";

    /// <summary> Gets the event emitted when the action provider observes the compile refresh starting. </summary>
    public const string RefreshStarted = "compile.refresh.started";

    /// <summary> Gets the event emitted after endpoint re-registration reconnects to the same execution. </summary>
    public const string Recovered = "compile.recovered";

    /// <summary> Gets the event emitted when the compile handler observes a normalized diagnostic. </summary>
    public const string Diagnostic = "compile.diagnostic";

    /// <summary> Gets the event emitted after the terminal compile result has been built. </summary>
    public const string Completed = "compile.completed";
}
