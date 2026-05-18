using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;

/// <summary> Represents verify profile resolution output. </summary>
internal sealed record VerifyProfileResolutionResult (
    VerifyProfileDefinition? Profile,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether profile resolution succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful profile resolution result. </summary>
    public static VerifyProfileResolutionResult Success (VerifyProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new VerifyProfileResolutionResult(profile, null);
    }

    /// <summary> Creates a failed profile resolution result. </summary>
    public static VerifyProfileResolutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new VerifyProfileResolutionResult(null, error);
    }
}
