using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Hosting.Cli.Play.Contracts;

/// <summary>
/// Represents an action-owned blocked or timed-out Play Mode transition result.
/// </summary>
internal sealed record PlayTransitionFailureErrorCommandPayload
    : PlayTransitionEvidenceErrorCommandPayload<
        ExecutionRef,
        PlayTransitionFailureCommandOutput>
{
    public PlayTransitionFailureErrorCommandPayload (
        ProjectIdentityInfo project,
        ExecutionRef lifecycleExecutionRef,
        ExecutionApplicationState applicationState,
        PlayLifecycleSnapshotOutput lifecycle,
        PlayTransitionFailureCommandOutput transition,
        int timeoutMilliseconds)
        : base(
            project,
            lifecycleExecutionRef,
            applicationState,
            lifecycle,
            transition,
            timeoutMilliseconds)
    {
    }
}
