using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Contracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Hosting.Cli.Daemon;

/// <summary> Projects daemon application output values to stable CLI JSON contract literals. </summary>
internal static class DaemonCommandOutputProjector
{
    public static string ToCleanupStatus (DaemonCleanupStatus status)
    {
        return ContractLiteralCodec.TryToValue(status, out var value)
            ? value
            : throw new InvalidOperationException($"Unsupported daemon cleanup status: {status}.");
    }

    public static string? ToCleanupSkipReason (DaemonCleanupSkipReason skipReason)
    {
        if (skipReason == DaemonCleanupSkipReason.None)
        {
            return null;
        }

        return ContractLiteralCodec.TryToValue(skipReason, out var value)
            ? value
            : throw new InvalidOperationException($"Unsupported daemon cleanup skip reason: {skipReason}.");
    }

    public static string? ToListCompletionReason (DaemonListCompletionReason? reason)
    {
        if (reason is null)
        {
            return null;
        }

        return ContractLiteralCodec.TryToValue(reason.Value, out var value)
            ? value
            : throw new InvalidOperationException($"Unsupported daemon-list completion reason: {reason.Value}.");
    }

    public static object ToListItem (DaemonListItemOutput item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new
        {
            worktreePath = item.WorktreePath,
            branchRef = item.BranchRef,
            head = item.Head,
            projectPath = item.ProjectPath,
            projectFingerprint = item.ProjectFingerprint,
            state = ToListState(item.State),
            reason = ToListReason(item.Reason),
            issuedAtUtc = item.IssuedAtUtc,
            processId = item.ProcessId,
            processStartedAtUtc = item.ProcessStartedAtUtc,
            editorMode = item.EditorMode,
            ownerKind = item.OwnerKind,
            canShutdownProcess = item.CanShutdownProcess,
            endpointTransportKind = item.EndpointTransportKind,
            endpointAddress = item.EndpointAddress,
            lifecycleState = item.LifecycleState,
            blockingReason = item.BlockingReason,
            compileState = item.CompileState,
            compileGeneration = item.CompileGeneration,
            domainReloadGeneration = item.DomainReloadGeneration,
            canAcceptExecutionRequests = item.CanAcceptExecutionRequests,
            observedAtUtc = item.ObservedAtUtc,
            actionRequired = item.ActionRequired,
            primaryDiagnostic = item.PrimaryDiagnostic,
            diagnosis = item.Diagnosis,
        };
    }

    public static string ToStartStatus (DaemonStartStatus status)
    {
        return ContractLiteralCodec.TryToValue(status, out var value)
            ? value
            : throw new InvalidOperationException($"Unsupported daemon start status: {status}.");
    }

    public static string ToStopStatus (DaemonStopStatus status)
    {
        return ContractLiteralCodec.TryToValue(status, out var value)
            ? value
            : throw new InvalidOperationException($"Unsupported daemon stop status: {status}.");
    }

    private static string ToListState (DaemonListItemState state)
    {
        return ContractLiteralCodec.TryToValue(state, out var value)
            ? value
            : throw new InvalidOperationException($"Unsupported daemon-list item state: {state}.");
    }

    private static string? ToListReason (DaemonListItemReason? reason)
    {
        if (reason is null)
        {
            return null;
        }

        return ContractLiteralCodec.TryToValue(reason.Value, out var value)
            ? value
            : throw new InvalidOperationException($"Unsupported daemon-list item reason: {reason.Value}.");
    }
}
