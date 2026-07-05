using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StaticProjectContextResolver : IProjectContextResolver
{
    private readonly ProjectContextResolutionResult result;

    private readonly List<string?> projectPaths = [];
    private readonly List<Invocation> invocations = [];

    public StaticProjectContextResolver (ProjectContextResolutionResult result)
    {
        this.result = result;
    }

    public IReadOnlyList<string?> ProjectPaths => projectPaths;

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<ProjectContextResolutionResult> ResolveAsync (
        string? projectPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        projectPaths.Add(projectPath);
        invocations.Add(new Invocation(projectPath, cancellationToken));
        return ValueTask.FromResult(result);
    }

    internal readonly record struct Invocation (
        string? ProjectPath,
        CancellationToken CancellationToken);
}
