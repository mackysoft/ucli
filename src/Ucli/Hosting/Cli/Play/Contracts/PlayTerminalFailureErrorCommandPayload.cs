using MackySoft.Ucli.Application.Features.Play.Common.Contracts;

using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Hosting.Cli.Play.Contracts;

/// <summary>
/// Represents a successful Play Mode result retained by a published,
/// non-completed Terminal Record.
/// </summary>
internal sealed record PlayTerminalFailureErrorCommandPayload
    : PlayTransitionEvidenceErrorCommandPayload<
        ITerminalExecutionRef,
        PlayTransitionSuccessOutput>
{
    public PlayTerminalFailureErrorCommandPayload (
        ProjectIdentityInfo project,
        ITerminalExecutionRef lifecycleExecutionRef,
        ExecutionApplicationState applicationState,
        PlayLifecycleSnapshotOutput lifecycle,
        PlayTransitionSuccessOutput transition,
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
