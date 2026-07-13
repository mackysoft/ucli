namespace MackySoft.Ucli.Contracts.Testing;

/// <summary> Represents the <c>test.case.finished</c> stream payload. </summary>
public readonly record struct TestCaseFinishedEntry (
    Guid RunId,
    string TestId,
    string TestName,
    string? AssemblyName,
    string TestPlatform,
    string[] Categories,
    string Result,
    long DurationMilliseconds,
    string? Message,
    string? StackTrace);
