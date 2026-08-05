using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Shared.Context;

/// <summary> Resolves shared project/config context used by command preflight flows. </summary>
internal interface IProjectContextResolver
{
    /// <summary> Resolves Unity project and config values into a shared command context. </summary>
    /// <param name="projectPath"> The optional normalized <c>--projectPath</c> value. When <see langword="null" />, project-path source precedence is applied. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the context-resolution result that contains either a fully resolved context or a structured error. </returns>
    ValueTask<ProjectContextResolutionResult> ResolveAsync (
        AbsolutePath? projectPath,
        CancellationToken cancellationToken = default);
}
