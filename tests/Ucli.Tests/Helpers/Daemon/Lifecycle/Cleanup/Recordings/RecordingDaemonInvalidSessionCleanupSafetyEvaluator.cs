using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingDaemonInvalidSessionCleanupSafetyEvaluator : IDaemonInvalidSessionCleanupSafetyEvaluator
{
    private readonly List<Invocation> invocations = [];

    public bool RequiresUnsafeSkipResult { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public bool RequiresUnsafeSkip (
        ResolvedUnityProjectContext unityProject,
        DaemonSession? session)
    {
        ArgumentNullException.ThrowIfNull(unityProject);

        invocations.Add(new Invocation(unityProject, session));

        return RequiresUnsafeSkipResult;
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        DaemonSession? Session);
}
