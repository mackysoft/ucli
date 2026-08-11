using MackySoft.Ucli.Application.Shared.Paths;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;

/// <summary> Represents normalized inputs for the <c>verify</c> assurance command. </summary>
internal sealed record VerifyCommandInput (
    AbsolutePath? ProjectPath,
    string? Profile,
    FilePathReference? ProfilePath,
    FilePathReference? FromPath,
    UnityExecutionMode? Mode,
    int? TimeoutMilliseconds);
