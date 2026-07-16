namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingUnityGuiEditorProcessLauncher : IUnityGuiEditorProcessLauncher
{
    private readonly List<Invocation> invocations = [];

    public UnityDaemonLaunchResult? NextResult { get; set; }

    public Func<ResolvedUnityProjectContext, string, CancellationToken, ValueTask<UnityDaemonLaunchResult>>? Handler { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<UnityDaemonLaunchResult> LaunchAsync (
        ResolvedUnityProjectContext unityProject,
        string unityLogPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(unityProject, unityLogPath, cancellationToken));
        if (Handler is not null)
        {
            return Handler(unityProject, unityLogPath, cancellationToken);
        }

        return ValueTask.FromResult(NextResult
            ?? throw new InvalidOperationException("A GUI Editor process launch result must be configured before launch."));
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        string UnityLogPath,
        CancellationToken CancellationToken);
}
