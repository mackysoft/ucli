using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Play.Common;

/// <summary> Represents the result of resolving a Play Mode lifecycle command context. </summary>
/// <param name="Context"> The resolved command context on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error on failure; otherwise <see langword="null" />. </param>
internal sealed record PlayCommandExecutionContextResolutionResult (
    PlayCommandExecutionContext? Context,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether context resolution succeeded. </summary>
    public bool IsSuccess => Context is not null && Error is null;

    /// <summary> Creates a successful context-resolution result. </summary>
    /// <param name="context"> The resolved command context. </param>
    /// <returns> The successful result. </returns>
    public static PlayCommandExecutionContextResolutionResult Success (PlayCommandExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new PlayCommandExecutionContextResolutionResult(context, null);
    }

    /// <summary> Creates a failed context-resolution result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed result. </returns>
    public static PlayCommandExecutionContextResolutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new PlayCommandExecutionContextResolutionResult(null, error);
    }
}
