namespace MackySoft.Ucli.Contracts.Testing;

/// <summary> Represents the <c>test.case.started</c> stream payload. </summary>
public sealed record TestCaseStartedEntry (
    string RunId,
    string TestId,
    string TestName,
    string? AssemblyName,
    string TestPlatform,
    string[] Categories);
