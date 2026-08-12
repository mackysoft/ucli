using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Application.Shared.Identifiers;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Application.Features.Programs.Supervision;

/// <summary>
/// Composes non-public Program use cases from caller-supplied ports without a
/// default port or public command route. Port implementations own concrete
/// Step dispatch and reconnect behavior.
/// </summary>
internal sealed record ProgramInternalUseCases (
    ProgramRunStartService RunStart,
    ProgramAttachedSupervisor Supervisor,
    ProgramRunStatusCancelReconciliationService StatusCancel);

/// <summary> Creates all internal Program use cases without creating a public command route. </summary>
internal static class ProgramInternalUseCaseComposition
{
    public static ProgramInternalUseCases Create (
        IProgramRunStoreFactory storeFactory,
        ProgramRunPersistenceService persistence,
        IProgramStepExecutionPort executionPort,
        IProgramRunStartNotificationPort startNotification,
        IProcessIdentityObserver processIdentityObserver,
        IGuidGenerator guidGenerator,
        TimeProvider timeProvider,
        ProcessIdentity owner)
    {
        ArgumentNullException.ThrowIfNull(storeFactory);
        ArgumentNullException.ThrowIfNull(persistence);
        ArgumentNullException.ThrowIfNull(executionPort);
        ArgumentNullException.ThrowIfNull(startNotification);
        ArgumentNullException.ThrowIfNull(processIdentityObserver);
        ArgumentNullException.ThrowIfNull(guidGenerator);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(owner);

        var supervisor = new ProgramAttachedSupervisor(storeFactory, executionPort, processIdentityObserver, guidGenerator, timeProvider, owner);
        return new ProgramInternalUseCases(
            new ProgramRunStartService(persistence, startNotification, supervisor, storeFactory, timeProvider),
            supervisor,
            new ProgramRunStatusCancelReconciliationService(storeFactory, processIdentityObserver, timeProvider));
    }
}
