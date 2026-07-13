namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>compile.started</c> stream payload. </summary>
public sealed record CompileStartedEntry (
    string RunId,
    ProjectFingerprint ProjectFingerprint,
    string RequestedMode,
    string ResolvedMode,
    string SessionKind,
    int TimeoutMilliseconds);
