namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents one <c>build.diagnostic</c> stream payload. </summary>
public sealed record BuildDiagnosticEntry (
    Guid RunId,
    string Code,
    string Severity,
    string Message,
    string Phase);
