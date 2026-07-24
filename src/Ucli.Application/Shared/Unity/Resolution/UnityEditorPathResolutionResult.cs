using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Shared.Unity.Resolution;

/// <summary> Represents one Unity-editor-path resolution result. </summary>
/// <param name="UnityEditorPath"> The resolved editor path on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error on failure; otherwise <see langword="null" />. </param>
internal sealed record UnityEditorPathResolutionResult (
    AbsolutePath? UnityEditorPath,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether resolution succeeded. </summary>
    public bool IsSuccess => UnityEditorPath is not null && Error is null;

    /// <summary> Creates a successful editor-path resolution result. </summary>
    /// <param name="unityEditorPath"> The resolved editor path. </param>
    /// <returns> The successful result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityEditorPath" /> is <see langword="null" />. </exception>
    public static UnityEditorPathResolutionResult Success (AbsolutePath unityEditorPath)
    {
        ArgumentNullException.ThrowIfNull(unityEditorPath);
        return new UnityEditorPathResolutionResult(unityEditorPath, null);
    }

    /// <summary> Creates a failed editor-path resolution result. </summary>
    /// <param name="error"> The structured resolution error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static UnityEditorPathResolutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UnityEditorPathResolutionResult(null, error);
    }
}
