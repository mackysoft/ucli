using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingDaemonReachabilityProbe : IDaemonReachabilityProbe
{
    private readonly List<Invocation> invocations = [];

    public RecordingDaemonReachabilityProbe (DaemonReachabilityProbeResult result)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public DaemonReachabilityProbeResult Result { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonReachabilityProbeResult> ProbeAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(unityProject, timeout, cancellationToken));
        return ValueTask.FromResult(Result);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        TimeSpan Timeout,
        CancellationToken CancellationToken);
}
