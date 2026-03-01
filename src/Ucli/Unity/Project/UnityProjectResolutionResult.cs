using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.UnityProject;

/// <summary> Represents the result of UnityProject path resolution. </summary>
/// <param name="Context"> The resolved UnityProject context, or <see langword="null" /> on failure. </param>
/// <param name="Error"> The structured resolution error, or <see langword="null" /> on success. </param>
internal sealed record UnityProjectResolutionResult (
    ResolvedUnityProjectContext? Context,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the resolution succeeded. </summary>
    public bool IsSuccess => Context is not null && Error is null;

    /// <summary> Creates a successful UnityProject resolution result. </summary>
    /// <param name="context"> The resolved UnityProject context. </param>
    /// <returns> The successful resolution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="context" /> is <see langword="null" />. </exception>
    public static UnityProjectResolutionResult Success (ResolvedUnityProjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new UnityProjectResolutionResult(context, null);
    }

    /// <summary> Creates a failed UnityProject resolution result. </summary>
    /// <param name="error"> The structured resolution error. </param>
    /// <returns> The failed resolution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static UnityProjectResolutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UnityProjectResolutionResult(null, error);
    }
}