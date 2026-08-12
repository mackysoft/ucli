using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;

namespace MackySoft.Ucli.Application.Features.Programs.Supervision;

/// <summary> Registers a Run, publishes its creation notice, and then lets its attached owner begin the first Step. </summary>
internal sealed class ProgramRunStartService
{
    private readonly ProgramRunPersistenceService persistence;
    private readonly IProgramRunStartNotificationPort startNotification;
    private readonly ProgramAttachedSupervisor supervisor;
    private readonly IProgramRunStoreFactory storeFactory;
    private readonly TimeProvider timeProvider;
    private readonly ProgramRunTerminalizer terminalizer;

    public ProgramRunStartService (
        ProgramRunPersistenceService persistence,
        IProgramRunStartNotificationPort startNotification,
        ProgramAttachedSupervisor supervisor,
        IProgramRunStoreFactory storeFactory,
        TimeProvider timeProvider)
    {
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.startNotification = startNotification ?? throw new ArgumentNullException(nameof(startNotification));
        this.supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
        this.storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        terminalizer = new ProgramRunTerminalizer(timeProvider);
    }

    public async ValueTask<ProgramRunRecord> StartAsync (
        ProgramRunRegistrationRequest request,
        ExecutionDeadline runDeadline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(runDeadline);
        var registration = await persistence.RegisterAsync(request, cancellationToken).ConfigureAwait(false);
        var run = registration.Current;
        if (!registration.Created)
        {
            return run;
        }

        try
        {
            await startNotification.NotifyAsync(run, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return await HandleNotificationFailureAsync(request.StorageProject, run, CancellationToken.None).ConfigureAwait(false);
        }
        return await supervisor.StartNextAsync(request.StorageProject, run.RunId, runDeadline, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Registered Program Run disappeared before its first Step could start.");
    }

    /// <summary> Finalizes an already registered Run when its mandatory creation notice could not be published. </summary>
    internal ValueTask<ProgramRunRecord> HandleNotificationFailureAsync (
        ResolvedUnityProjectContext project,
        ProgramRunRecord run,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(run);
        return terminalizer.TerminalizeAsync(
            storeFactory.ForProject(project), run, ProgramRunState.Failed,
            "PROGRAM_START_EVENT_WRITE_FAILED", cancellationToken);
    }
}
