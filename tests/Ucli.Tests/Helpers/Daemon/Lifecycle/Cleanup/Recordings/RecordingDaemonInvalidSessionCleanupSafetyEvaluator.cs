using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingDaemonInvalidSessionCleanupSafetyEvaluator : IDaemonInvalidSessionCleanupSafetyEvaluator
{
    private readonly List<DaemonInvalidSessionEvidence?> invocations = [];

    public bool RequiresUnsafeSkipResult { get; set; }

    public IReadOnlyList<DaemonInvalidSessionEvidence?> Invocations => invocations;

    public bool RequiresUnsafeSkip (DaemonInvalidSessionEvidence? evidence)
    {
        invocations.Add(evidence);

        return RequiresUnsafeSkipResult;
    }
}
