using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingUnityProjectResolver : IUnityProjectResolver
{
    private readonly Dictionary<AbsolutePath, UnityProjectResolutionResult>? resultsByPath;
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
            static context => context.UnityProjectRoot,
            static context => UnityProjectResolutionResult.Success(context));
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
            var currentDirectory = AbsolutePath.Parse(Environment.CurrentDirectory);
            if (AbsolutePath.TryResolve(
                    currentDirectory,
                    projectPathCandidate.Path,
                    out var guardedPath,
                    out _)
                && resultsByPath.TryGetValue(guardedPath, out var mappedResult))
            {
                return mappedResult;
            }

            return UnityProjectResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"UnityProject path does not exist: {projectPathCandidate.Path}",
                ProjectContextErrorCodes.ProjectPathNotFound));
        }

        return result!;
    }

    public UnityProjectResolutionResult Resolve (
        AbsolutePath unityProjectRoot,
        UnityProjectPathSource source,
        string? sourceLabel = null)
    {
        ArgumentNullException.ThrowIfNull(unityProjectRoot);

        var projectPathCandidate = new ProjectPathCandidate(
            unityProjectRoot.Value,
            source,
            sourceLabel);
        invocations.Add(new Invocation(projectPathCandidate));

        if (handler is not null)
        {
            return handler(projectPathCandidate);
        }

        if (resultsByPath is not null)
        {
            if (resultsByPath.TryGetValue(unityProjectRoot, out var mappedResult))
            {
                return mappedResult;
            }

            return UnityProjectResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"UnityProject path does not exist: {unityProjectRoot.Value}",
                ProjectContextErrorCodes.ProjectPathNotFound));
        }

        return result!;
    }

    internal readonly record struct Invocation (ProjectPathCandidate ProjectPathCandidate);
}
