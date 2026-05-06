namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary> Resolves the effective Unity project path candidate from command options, environment variables, and fallbacks. </summary>
internal interface IProjectPathInputResolver
{
    /// <summary> Resolves one effective project path candidate with source metadata. </summary>
    /// <param name="input"> The unresolved project-path inputs. </param>
    /// <returns> The selected project path candidate. </returns>
    ProjectPathCandidate Resolve (ProjectContextResolutionInput input);
}
