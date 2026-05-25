namespace MackySoft.Ucli.Contracts.Testing;

/// <summary> Represents the <c>test.run.diagnostic</c> stream payload. </summary>
public sealed record TestRunDiagnosticEntry (
    string RunId,
    string Code,
    string Message,
    string Severity);
