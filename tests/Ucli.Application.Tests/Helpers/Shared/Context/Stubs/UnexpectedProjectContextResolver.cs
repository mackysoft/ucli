using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class UnexpectedProjectContextResolver : IProjectContextResolver
{
    public ValueTask<ProjectContextResolutionResult> ResolveAsync (
        AbsolutePath? projectPath,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Project context should not be resolved.");
    }
}
