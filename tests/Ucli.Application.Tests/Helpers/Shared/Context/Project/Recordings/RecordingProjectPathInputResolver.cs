namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingProjectPathInputResolver : IProjectPathInputResolver
{
    private readonly Func<ProjectContextResolutionInput, ProjectPathCandidate> resolve;

    private readonly List<Invocation> invocations = [];

    public RecordingProjectPathInputResolver (Func<string?, string?, string?> resolve)
        : this(input => ResolveCandidate(input, resolve))
    {
    }

    public RecordingProjectPathInputResolver (Func<ProjectContextResolutionInput, ProjectPathCandidate> resolve)
    {
        this.resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ProjectPathCandidate Resolve (ProjectContextResolutionInput input)
    {
        var projectPathCandidate = resolve(input);
        invocations.Add(new Invocation(input, projectPathCandidate));
        return projectPathCandidate;
    }

    private static ProjectPathCandidate ResolveCandidate (
        ProjectContextResolutionInput input,
        Func<string?, string?, string?> resolve)
    {
        var resolvedPath = resolve(input.CommandOptionProjectPath, input.FallbackProjectPath);
        return new ProjectPathCandidate(
            resolvedPath ?? Environment.CurrentDirectory,
            ResolveSource(input, resolvedPath));
    }

    private static UnityProjectPathSource ResolveSource (
        ProjectContextResolutionInput input,
        string? resolvedPath)
    {
        if (string.Equals(resolvedPath, input.CommandOptionProjectPath, StringComparison.Ordinal))
        {
            return UnityProjectPathSource.CommandOption;
        }

        if (string.Equals(resolvedPath, input.FallbackProjectPath, StringComparison.Ordinal))
        {
            return UnityProjectPathSource.Fallback;
        }

        if (!string.IsNullOrEmpty(resolvedPath))
        {
            return UnityProjectPathSource.EnvironmentVariable;
        }

        return UnityProjectPathSource.CurrentDirectory;
    }

    internal readonly record struct Invocation (
        ProjectContextResolutionInput Input,
        ProjectPathCandidate ProjectPathCandidate);
}
