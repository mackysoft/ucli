namespace MackySoft.Ucli.Contracts.Testing;

/// <summary> Represents the <c>test.run.diagnostic</c> stream payload. </summary>
public readonly record struct TestRunDiagnosticEntry (
    Guid RunId,
    string Code,
    string Message,
    string Severity);
