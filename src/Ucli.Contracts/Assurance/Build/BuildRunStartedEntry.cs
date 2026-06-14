namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>build.run.started</c> stream payload. </summary>
public sealed record BuildRunStartedEntry (
    string RunId,
    string ProjectFingerprint,
    string RequestedMode,
    string ResolvedMode,
    string SessionKind,
    int TimeoutMilliseconds,
    string BuildTarget,
    string OutputPath);
