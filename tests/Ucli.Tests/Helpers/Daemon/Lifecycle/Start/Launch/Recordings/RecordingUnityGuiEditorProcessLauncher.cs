namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingUnityGuiEditorProcessLauncher : IUnityGuiEditorProcessLauncher
{
    private readonly List<Invocation> invocations = [];

    public UnityDaemonLaunchResult NextResult { get; set; } = UnityDaemonLaunchResult.Success(2000, DateTimeOffset.UtcNow);

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<UnityDaemonLaunchResult> LaunchAsync (
        ResolvedUnityProjectContext unityProject,
        string unityLogPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(unityProject, unityLogPath, cancellationToken));
        return ValueTask.FromResult(NextResult);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        string UnityLogPath,
        CancellationToken CancellationToken);
}
