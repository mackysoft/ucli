using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary> Resolves and validates a UnityProject path for CLI command execution. </summary>
internal interface IUnityProjectResolver
{
    /// <summary> Resolves the UnityProject context from a selected project-path candidate. </summary>
    /// <param name="projectPathCandidate"> The selected but not yet normalized project-path candidate. </param>
    /// <returns> The resolution result containing either a validated UnityProject context or a structured error. </returns>
    UnityProjectResolutionResult Resolve (ProjectPathCandidate projectPathCandidate);

    /// <summary> Resolves UnityProject context from an already guarded absolute path. </summary>
    /// <param name="unityProjectRoot"> The guarded Unity project root. </param>
    /// <param name="source"> The source that selected the path. </param>
    /// <param name="sourceLabel"> The optional source label used when the source has a command-specific identity. </param>
    /// <returns> The resolution result containing either a validated UnityProject context or a structured error. </returns>
    UnityProjectResolutionResult Resolve (
        AbsolutePath unityProjectRoot,
        UnityProjectPathSource source,
        string? sourceLabel = null);
}
