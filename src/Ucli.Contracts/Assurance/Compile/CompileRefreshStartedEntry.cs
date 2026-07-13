namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>compile.refresh.started</c> stream payload. </summary>
public sealed record CompileRefreshStartedEntry (
    Guid RunId,
    string RefreshOrigin,
    string ObservationSource);
