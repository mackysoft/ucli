namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;

/// <summary> Represents normalized inputs for the <c>verify</c> assurance command. </summary>
internal sealed record VerifyCommandInput (
    string? ProjectPath,
    string? Profile,
    string? ProfilePath,
    string? FromPath,
    UnityExecutionMode? Mode,
    int? TimeoutMilliseconds);
