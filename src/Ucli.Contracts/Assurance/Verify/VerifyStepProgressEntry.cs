namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents a verify profile step progress stream payload. </summary>
public sealed record VerifyStepProgressEntry (
    string Kind,
    bool Required,
    string[] Effects,
    string? SkipReason);
