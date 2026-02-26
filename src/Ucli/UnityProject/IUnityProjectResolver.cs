namespace MackySoft.Ucli.UnityProject;

/// <summary> Resolves and validates a UnityProject path for CLI command execution. </summary>
internal interface IUnityProjectResolver
{
    /// <summary> Resolves the UnityProject context from an optional <c>--projectPath</c> argument. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> value. When <see langword="null" />, empty, or whitespace, the current working directory is used. </param>
    /// <returns> The resolution result containing either a validated UnityProject context or a structured error. </returns>
    UnityProjectResolutionResult Resolve (string? projectPath);
}