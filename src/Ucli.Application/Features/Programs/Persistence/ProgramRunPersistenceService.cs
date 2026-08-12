using MackySoft.Ucli.Application.Features.Programs.Resolution;
using MackySoft.Ucli.Application.Shared.Identifiers;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Application.Features.Programs.Persistence;

/// <summary> Provides registration, readback, and cancellation persistence without executing Program Steps. </summary>
internal sealed class ProgramRunPersistenceService
{
    private readonly IProgramRunStoreFactory storeFactory;
    private readonly IGuidGenerator guidGenerator;
    private readonly TimeProvider timeProvider;

    public ProgramRunPersistenceService (IProgramRunStoreFactory storeFactory, IGuidGenerator guidGenerator, TimeProvider timeProvider)
    {
        this.storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
        this.guidGenerator = guidGenerator ?? throw new ArgumentNullException(nameof(guidGenerator));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async ValueTask<ProgramRunStoreCreateResult> RegisterAsync (ProgramRunRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        var definitionSnapshot = ProgramDefinitionSnapshot.FromResolved(request.ResolvedDefinition);
        request.ValidatePendingSteps(definitionSnapshot.RestoreFixedDefinition());
        var runId = guidGenerator.Generate();
        if (runId == Guid.Empty)
        {
            throw new InvalidOperationException("Program Run identifier generator returned an empty identifier.");
        }

        var now = timeProvider.GetUtcNow();
        var store = storeFactory.ForProject(request.StorageProject);
        var definitionSnapshotRef = await store.PublishDefinitionSnapshotAsync(runId, definitionSnapshot, cancellationToken).ConfigureAwait(false);
        var run = new ProgramRunRecord(
            ProgramRunRecord.CurrentSchemaVersion, 0, runId, definitionSnapshot.DefinitionDigest, definitionSnapshotRef,
            request.Project, request.FixedContext, request.Host, request.StartedGeneration, request.CurrentEditorGeneration,
            request.DeadlineUtc, now, now, ProgramRunState.Created, 0,
            request.PendingSteps.Select(static step => step.ToRecord()).ToArray(), [], ProgramCancellationRecord.None, null);
        return await store.CreateAsync(run, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<ProgramRunStoredDefinition?> LoadAsync (ResolvedUnityProjectContext project, Guid runId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        return storeFactory.ForProject(project).ReadDefinitionAsync(runId, cancellationToken);
    }

    public async ValueTask<ProgramRunRecord?> RequestCancellationAsync (
        ResolvedUnityProjectContext project, Guid runId, string? reasonCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        var store = storeFactory.ForProject(project);
        while (true)
        {
            var current = await store.ReadAsync(runId, cancellationToken).ConfigureAwait(false);
            if (current is null || ProgramRunStateSemantics.IsTerminal(current.State) || current.Cancellation.Requested)
            {
                return current;
            }

            var replacement = new ProgramRunRecord(
                current.SchemaVersion, current.Version + 1, current.RunId, current.DefinitionDigest, current.DefinitionSnapshotRef,
                current.Project, current.FixedContext, current.Host, current.StartedGeneration, current.CurrentEditorGeneration,
                current.DeadlineUtc, current.StartedAtUtc, timeProvider.GetUtcNow(), current.State, current.Cursor,
                current.Steps, current.ChildExecutionRefs, current.Cancellation.Request(timeProvider.GetUtcNow(), reasonCode), current.TerminalRecordRef);
            var exchange = await store.CompareExchangeAsync(current, replacement, cancellationToken).ConfigureAwait(false);
            if (exchange.Exchanged || exchange.Current.Cancellation.Requested || ProgramRunStateSemantics.IsTerminal(exchange.Current.State))
            {
                return exchange.Current;
            }
        }
    }
}

/// <summary> Opens the Program Run persistence boundary for one resolved project repository. </summary>
internal interface IProgramRunStoreFactory
{
    IProgramRunStore ForProject (ResolvedUnityProjectContext project);
}

/// <summary> Supplies the fully resolved immutable inputs admitted before Program planning begins. </summary>
internal sealed record ProgramRunRegistrationRequest (
    ResolvedUnityProjectContext StorageProject,
    UnityProjectIdentity Project,
    ResolvedProgramDefinition ResolvedDefinition,
    ProgramRunFixedContext FixedContext,
    LifecycleExecutionHostRegistration Host,
    UnityEditorGenerationSnapshot StartedGeneration,
    UnityEditorGenerationSnapshot? CurrentEditorGeneration,
    DateTimeOffset DeadlineUtc,
    IReadOnlyList<ProgramRunPendingStep> PendingSteps)
{
    public void Validate ()
    {
        if (StorageProject is null || Project is null || ResolvedDefinition is null || FixedContext is null
            || Host is null || StartedGeneration is null || PendingSteps is null || PendingSteps.Count == 0
            || DeadlineUtc == default || DeadlineUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Program Run registration requires one resolved definition, fixed execution context, and pending Steps.");
        }
        if (Project.ProjectFingerprint != StorageProject.ProjectFingerprint)
        {
            throw new ArgumentException("Program Run identity must use the resolved storage project.");
        }
        foreach (var step in PendingSteps)
        {
            step.Validate();
        }
    }

    public void ValidatePendingSteps (ProgramDefinitionSnapshotFixedDefinition fixedDefinition)
    {
        ProgramRunDefinitionBinding.Validate(PendingSteps.Select(static step => step.ToRecord()).ToArray(), FixedContext, fixedDefinition);
    }
}

/// <summary> Captures one fully resolved pending Step before any planning or side effect begins. </summary>
internal sealed record ProgramRunPendingStep (string Command, int TimeoutMilliseconds)
{
    public void Validate ()
    {
        if (string.IsNullOrWhiteSpace(Command) || TimeoutMilliseconds < 1)
        {
            throw new ArgumentException("Program pending Step requires a command and positive timeout.");
        }
    }

    public ProgramRunStepRecord ToRecord () => new(
        Command, TimeoutMilliseconds, ProgramStepState.Deferred, null, null, null, null, null,
        ExecutionApplicationState.NotApplied, null, [], null, null, null, null, null, [], null, null, null);
}
