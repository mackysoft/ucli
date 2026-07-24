using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Hosting.Cli.Daemon;

/// <summary> Projects daemon application output values to stable CLI JSON contract literals. </summary>
internal static class DaemonCommandOutputProjector
{
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
            state = TextVocabulary.GetText(item.State),
            reason = item.Reason.HasValue
                ? TextVocabulary.GetText(item.Reason.Value)
                : null,
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
            generations = item.Generations,
            canAcceptExecutionRequests = item.CanAcceptExecutionRequests,
            observedAtUtc = item.ObservedAtUtc,
            actionRequired = item.ActionRequired,
            primaryDiagnostic = item.PrimaryDiagnostic,
            diagnosis = item.Diagnosis,
        };
    }
}
