using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingDaemonSessionDiagnosisResolver : IDaemonSessionDiagnosisResolver
{
    private readonly List<Invocation> invocations = [];

    public DaemonDiagnosis? Diagnosis { get; set; }

    public Func<ResolvedUnityProjectContext, DaemonSession, DaemonDiagnosis?, CancellationToken, ValueTask<DaemonDiagnosis?>>? Handler { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonDiagnosis?> ResolveForSessionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        DaemonDiagnosis? persistedDiagnosis,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);
        invocations.Add(new Invocation(
            unityProject,
            session,
            persistedDiagnosis,
            cancellationToken));

        if (Handler != null)
        {
            return Handler(unityProject, session, persistedDiagnosis, cancellationToken);
        }

        return ValueTask.FromResult(Diagnosis);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        DaemonSession Session,
        DaemonDiagnosis? PersistedDiagnosis,
        CancellationToken CancellationToken);
}
