using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingUnityProjectResolver : IUnityProjectResolver
{
    private readonly Dictionary<string, UnityProjectResolutionResult>? resultsByPath;
    private readonly UnityProjectResolutionResult? result;
    private readonly Func<ProjectPathCandidate, UnityProjectResolutionResult>? handler;

    private readonly List<Invocation> invocations = [];

    public RecordingUnityProjectResolver (UnityProjectResolutionResult result)
    {
        this.result = result ?? throw new ArgumentNullException(nameof(result));
    }

    private RecordingUnityProjectResolver (IEnumerable<ResolvedUnityProjectContext> contexts)
    {
        ArgumentNullException.ThrowIfNull(contexts);

        resultsByPath = contexts.ToDictionary(
            static context => Path.GetFullPath(context.UnityProjectRoot),
            static context => UnityProjectResolutionResult.Success(context),
            StringComparer.Ordinal);
    }

    private RecordingUnityProjectResolver (Func<ProjectPathCandidate, UnityProjectResolutionResult> handler)
    {
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public static RecordingUnityProjectResolver FromContexts (params ResolvedUnityProjectContext[] contexts)
    {
        return new RecordingUnityProjectResolver(contexts);
    }

    public static RecordingUnityProjectResolver FromHandler (Func<ProjectPathCandidate, UnityProjectResolutionResult> handler)
    {
        return new RecordingUnityProjectResolver(handler);
    }

    public UnityProjectResolutionResult Resolve (ProjectPathCandidate projectPathCandidate)
    {
        ArgumentNullException.ThrowIfNull(projectPathCandidate);

        invocations.Add(new Invocation(projectPathCandidate));

        if (handler is not null)
        {
            return handler(projectPathCandidate);
        }

        if (resultsByPath is not null)
        {
            var normalizedPath = Path.GetFullPath(projectPathCandidate.Path);
            if (resultsByPath.TryGetValue(normalizedPath, out var mappedResult))
            {
                return mappedResult;
            }

            return UnityProjectResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"UnityProject path does not exist: {projectPathCandidate.Path}",
                ProjectContextErrorCodes.ProjectPathNotFound));
        }

        return result!;
    }

    internal readonly record struct Invocation (ProjectPathCandidate ProjectPathCandidate);
}
