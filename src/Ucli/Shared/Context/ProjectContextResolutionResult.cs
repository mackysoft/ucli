using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Shared.Context;

/// <summary> Represents the result of resolving <see cref="ProjectContext" /> values. </summary>
/// <param name="Context"> The resolved context, or <see langword="null" /> on failure. </param>
/// <param name="Error"> The structured resolution error, or <see langword="null" /> on success. </param>
internal sealed record ProjectContextResolutionResult (
    ProjectContext? Context,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether context resolution succeeded. </summary>
    public bool IsSuccess => Context is not null && Error is null;

    /// <summary> Creates a successful context-resolution result. </summary>
    /// <param name="context"> The resolved context value. </param>
    /// <returns> The successful result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="context" /> is <see langword="null" />. </exception>
    public static ProjectContextResolutionResult Success (ProjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new ProjectContextResolutionResult(context, null);
    }

    /// <summary> Creates a failed context-resolution result. </summary>
    /// <param name="error"> The structured resolution error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static ProjectContextResolutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ProjectContextResolutionResult(null, error);
    }
}