namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>compile.recovered</c> stream payload. </summary>
public sealed record CompileRecoveredEntry (
    Guid RunId,
    string SummaryJsonPath,
    string? DispatchFailureCode,
    int PollAttempts);
