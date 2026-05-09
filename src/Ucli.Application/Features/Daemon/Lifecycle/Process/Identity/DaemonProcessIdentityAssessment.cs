using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;

/// <summary> Represents one daemon process identity assessment result. </summary>
/// <param name="Status"> The process identity assessment status. </param>
/// <param name="ObservedStartTimeUtc"> The observed process start time when available. </param>
/// <param name="Error"> The structured error when assessment is uncertain; otherwise <see langword="null" />. </param>
internal readonly record struct DaemonProcessIdentityAssessment (
    DaemonProcessIdentityAssessmentStatus Status,
    DateTimeOffset? ObservedStartTimeUtc,
    ExecutionError? Error);
