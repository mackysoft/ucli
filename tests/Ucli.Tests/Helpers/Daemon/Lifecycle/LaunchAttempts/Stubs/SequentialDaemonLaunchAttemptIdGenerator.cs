using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class SequentialDaemonLaunchAttemptIdGenerator : IDaemonLaunchAttemptIdGenerator
{
    private readonly List<DateTimeOffset> startedAtUtcValues = [];
    private int sequence;

    public IReadOnlyList<DateTimeOffset> StartedAtUtcValues => startedAtUtcValues;

    public string Create (DateTimeOffset startedAtUtc)
    {
        startedAtUtcValues.Add(startedAtUtc);
        sequence++;
        return $"20260312_000000Z_{sequence:00000000}";
    }
}
