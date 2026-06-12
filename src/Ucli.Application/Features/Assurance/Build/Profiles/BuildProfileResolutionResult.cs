using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents build profile resolution output. </summary>
internal sealed record BuildProfileResolutionResult (
    ResolvedBuildProfile? Profile,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether profile resolution succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful profile resolution result. </summary>
    public static BuildProfileResolutionResult Success (ResolvedBuildProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new BuildProfileResolutionResult(profile, null);
    }

    /// <summary> Creates a failed profile resolution result. </summary>
    public static BuildProfileResolutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new BuildProfileResolutionResult(null, error);
    }
}
