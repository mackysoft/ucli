namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary> Resolves and validates a UnityProject path for CLI command execution. </summary>
internal interface IUnityProjectResolver
{
    /// <summary> Resolves the UnityProject context from a selected project-path candidate. </summary>
    /// <param name="projectPathCandidate"> The selected but not yet normalized project-path candidate. </param>
    /// <returns> The resolution result containing either a validated UnityProject context or a structured error. </returns>
    UnityProjectResolutionResult Resolve (ProjectPathCandidate projectPathCandidate);
}
