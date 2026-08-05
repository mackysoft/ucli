namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingProjectPathInputResolver : IProjectPathInputResolver
{
    private readonly Func<ProjectContextResolutionInput, ProjectPathInputResolutionResult> resolve;

    private readonly List<Invocation> invocations = [];

    public RecordingProjectPathInputResolver (Func<ProjectContextResolutionInput, ProjectPathInputResolutionResult> resolve)
    {
        this.resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public static ProjectPathInputResolutionResult Success (
        AbsolutePath path,
        UnityProjectPathSource source) =>
        ProjectPathInputResolutionResult.Success(new ProjectPathCandidate(path, source));

    public ProjectPathInputResolutionResult Resolve (ProjectContextResolutionInput input)
    {
        var result = resolve(input);
        invocations.Add(new Invocation(input, result));
        return result;
    }

    internal readonly record struct Invocation (
        ProjectContextResolutionInput Input,
        ProjectPathInputResolutionResult Result);
}
