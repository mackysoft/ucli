namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>verify.started</c> and <c>verify.completed</c> stream payload. </summary>
public readonly record struct VerifyProgressEntry (
    string ProfileSource,
    string ProfileName,
    string? ProfilePath,
    string ProfileDigest,
    int StepCount,
    string? Verdict);
