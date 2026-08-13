using System.Text.Json;
using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Application.Features.Programs.Resolution;
using MackySoft.Ucli.Application.Features.Programs.Supervision;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Features.Programs.Supervision;

public sealed class ProgramSupervisorTests
{
    private static readonly DateTimeOffset StartedAtUtc = new(2026, 8, 12, 0, 0, 0, TimeSpan.Zero);
    private static readonly ProcessIdentity Owner = new(100, 10);

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartNextAsync_PersistsTheStepExecutionBeforeCallingTheClosedPort ()
    {
        var store = new MemoryStore(CreateRun());
        var port = new RecordingPort();
        var clock = new FakeTimeProvider(StartedAtUtc);
        var supervisor = CreateSupervisor(store, port, new FixedObserver(), clock);

        var result = await supervisor.StartNextAsync(CreateProject(), store.Current.RunId, ExecutionDeadline.Start(TimeSpan.FromMinutes(1), clock));

        Assert.Equal(["compareExchange", "compareExchange", "compareExchange", "start"], store.Operations.Concat(port.Operations));
        Assert.Equal(ProgramRunState.Running, result!.State);
        var started = Assert.Single(port.Starts);
        Assert.Equal(store.Current.RunId, started.Run.RunId);
        Assert.NotEqual(Guid.Empty, started.Execution.ExecutionId);
        Assert.Equal(ProgramStepState.Planning, started.Run.Steps[0].State);
        Assert.Equal(started.Execution, started.Run.Steps[0].Execution);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartNextAsync_WhenTransportResponseIsLost_RecoversTheSameLogicalExecutionWithoutAnotherStart ()
    {
        var store = new MemoryStore(CreateRun());
        var port = new RecordingPort { StartResult = ProgramStepExecutionPortResult.CommunicationLost };
        var clock = new FakeTimeProvider(StartedAtUtc);
        var supervisor = CreateSupervisor(store, port, new FixedObserver(), clock);

        await supervisor.StartNextAsync(CreateProject(), store.Current.RunId, ExecutionDeadline.Start(TimeSpan.FromMinutes(1), clock));

        var start = Assert.Single(port.Starts);
        var recovery = Assert.Single(port.Recoveries);
        Assert.Equal(start.Execution.ExecutionId, recovery.Execution.ExecutionId);
        Assert.Single(port.Starts);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartNextAsync_WhenTheTypedStartResponseIsTerminal_PublishesItAndAdvancesWithoutRecovery ()
    {
        var source = CreateRun();
        var second = new ProgramRunPendingStep("ready", 1000).ToRecord();
        var store = new MemoryStore(new ProgramRunRecord(
            source.SchemaVersion, source.Version, source.RunId, source.DefinitionDigest, source.DefinitionSnapshotRef,
            source.Project, source.FixedContext, source.Host, source.StartedGeneration, source.CurrentEditorGeneration,
            source.DeadlineUtc, source.StartedAtUtc, source.UpdatedAtUtc, source.State, source.Cursor,
            [source.Steps[0], second], source.ChildExecutionRefs, source.Cancellation, source.TerminalRecordRef));
        var port = new RecordingPort
        {
            OnStartAsync = static (start, _) => ValueTask.FromResult(
                start.StepIndex == 0
                    ? ProgramStepExecutionPortResult.TerminallyReturned(
                        new ProgramStepExecutionRecoveredTerminal(ProgramStepState.Completed, Verdict.Pass, ExecutionApplicationState.Applied, null))
                    : ProgramStepExecutionPortResult.Started),
        };
        var clock = new FakeTimeProvider(StartedAtUtc);
        var supervisor = CreateSupervisor(store, port, new FixedObserver(), clock);

        var result = await supervisor.StartNextAsync(
            CreateProject(), store.Current.RunId, ExecutionDeadline.Start(TimeSpan.FromMinutes(1), clock));

        Assert.Equal(ProgramRunState.Running, result!.State);
        Assert.Equal(ProgramStepState.Completed, result.Steps[0].State);
        Assert.Equal(ProgramStepState.Planning, result.Steps[1].State);
        Assert.Equal([0, 1], port.Starts.Select(static start => start.StepIndex));
        Assert.Empty(port.Recoveries);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartNextAsync_WhenCallPreflightFails_PublishesOneNotAppliedTerminalWithoutStartingRequestExecution ()
    {
        var source = CreateRun();
        var call = source.Steps[0] with { Command = "call" };
        var store = new MemoryStore(CreateRun(source, call));
        var port = new RecordingPort
        {
            StartResult = ProgramStepExecutionPortResult.TerminallyReturned(
                new ProgramStepExecutionRecoveredTerminal(
                    ProgramStepState.Failed,
                    null,
                    ExecutionApplicationState.NotApplied,
                    "PROGRAM_CALL_STATIC_PREFLIGHT_REJECTED",
                    Origin: ProgramStepExecutionTerminalOrigin.LocalPreflight)),
        };
        var clock = new FakeTimeProvider(StartedAtUtc);
        var supervisor = CreateSupervisor(store, port, new FixedObserver(), clock);

        var result = await supervisor.StartNextAsync(
            CreateProject(), store.Current.RunId, ExecutionDeadline.Start(TimeSpan.FromMinutes(1), clock));
        var recovered = await supervisor.RecoverAsync(
            CreateProject(), store.Current.RunId, ExecutionDeadline.Start(TimeSpan.FromMinutes(1), clock));

        var step = Assert.Single(result!.Steps);
        Assert.Equal(ProgramRunState.Failed, result.State);
        Assert.Equal(ProgramStepState.Failed, step.State);
        Assert.Equal(ExecutionApplicationState.NotApplied, step.ApplicationState);
        Assert.Equal("PROGRAM_CALL_STATIC_PREFLIGHT_REJECTED", step.ErrorCode);
        Assert.Null(step.RequestExecution);
        Assert.Null(step.RequestPlanRef);
        Assert.Empty(step.OperationDescriptorRefs);
        Assert.True(step.ExecutionPortInvoked);
        Assert.Single(port.Starts);
        Assert.Empty(port.Recoveries);
        Assert.Same(result, recovered);
        Assert.Equal(1, store.StepTerminalPublicationAttempts);
        Assert.NotNull(store.LastRunTerminal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartNextAsync_WhenLifecycleObserverPersistsItsReferenceBeforeTerminalReturn_PublishesFromThePersistedStep ()
    {
        var source = CreateRun();
        var refresh = source.Steps[0] with { Command = "refresh" };
        var store = new MemoryStore(CreateRun(source, refresh));
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        var terminalReference = new TerminalExecutionRef(
            definition.ExecutionKind,
            Guid.Parse("3a0f9ada-c23f-41b7-88ac-849cedf38882"),
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new ExecutionState("completed"),
            null,
            new PathArtifactRef(
                LifecycleExecutionArtifactContract.TerminalRecordKind,
                LifecycleExecutionArtifactContract.TerminalRecordMediaType,
                new ArtifactPath("lifecycle-execution/refresh/terminal.json"),
                Sha256Digest.Parse(new string('e', 64)),
                1,
                StartedAtUtc));
        var activeReference = new ActiveExecutionRef(
            definition.ExecutionKind,
            terminalReference.Id,
            terminalReference.DefinitionDigest,
            new ExecutionState("registered"),
            new ExecutionStatusLocator(".ucli/local/lifecycle-execution/refresh/execution.json"));
        var port = new RecordingPort
        {
            OnStartAsync = (start, _) =>
            {
                var observed = store.Current;
                store.Replace(ReplaceStep(observed, observed.Steps[start.StepIndex] with
                {
                    State = ProgramStepState.Running,
                    LifecycleExecutionRef = activeReference,
                    GenerationBefore = observed.StartedGeneration,
                }));
                return ValueTask.FromResult(ProgramStepExecutionPortResult.TerminallyReturned(
                    new ProgramStepExecutionRecoveredTerminal(
                        ProgramStepState.Completed,
                        null,
                        ExecutionApplicationState.Applied,
                        null,
                        observed.StartedGeneration,
                        terminalReference)));
            },
        };
        var clock = new FakeTimeProvider(StartedAtUtc);
        var supervisor = CreateSupervisor(store, port, new FixedObserver(), clock);

        var result = await supervisor.StartNextAsync(
            CreateProject(), store.Current.RunId, ExecutionDeadline.Start(TimeSpan.FromMinutes(1), clock));

        var step = Assert.Single(result!.Steps);
        Assert.Equal(ProgramRunState.Completed, result.State);
        Assert.Equal(ProgramStepState.Completed, step.State);
        Assert.Equal(terminalReference, step.LifecycleExecutionRef);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartNextAsync_PersistsPortAdmissionBeforeCallingThePort ()
    {
        var store = new MemoryStore(CreateRun());
        var port = new RecordingPort();
        var clock = new FakeTimeProvider(StartedAtUtc);
        var observer = new CountingObserver();
        var supervisor = CreateSupervisor(store, port, observer, clock);

        await supervisor.StartNextAsync(CreateProject(), store.Current.RunId, ExecutionDeadline.Start(TimeSpan.FromMinutes(1), clock));

        Assert.True(Assert.Single(port.Starts).Run.Steps[0].ExecutionPortInvoked);
        Assert.Equal(["compareExchange", "compareExchange", "compareExchange", "start"], store.Operations.Concat(port.Operations));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartNextAndRecoverAsync_WithAnotherOwnerLeaveTheRunAndPortsUntouched ()
    {
        var store = new MemoryStore(CreateRun());
        var port = new RecordingPort();
        var observer = new CountingObserver();
        var clock = new FakeTimeProvider(StartedAtUtc);
        var supervisor = new ProgramAttachedSupervisor(
            store, port, observer, new FixedGuidGenerator(Guid.NewGuid()), clock, new ProcessIdentity(101, 10));
        var deadline = ExecutionDeadline.Start(TimeSpan.FromMinutes(1), clock);

        var started = await supervisor.StartNextAsync(CreateProject(), store.Current.RunId, deadline);
        var recovered = await supervisor.RecoverAsync(CreateProject(), store.Current.RunId, deadline);

        Assert.Same(store.Current, started);
        Assert.Same(store.Current, recovered);
        Assert.Empty(store.Operations);
        Assert.Empty(port.Starts);
        Assert.Empty(port.Recoveries);
        Assert.Empty(port.Terminations);
        Assert.Empty(port.Operations);
        Assert.Equal(0, observer.ObservationCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatusAsync_WhenPlanningWasNotPortInvoked_TerminalizesOnlyTheRunAndRestoresDeferredStep ()
    {
        var pending = CreateRun();
        var plannedStep = pending.Steps[0] with
        {
            State = ProgramStepState.Planning,
            PlanningStartedAtUtc = StartedAtUtc,
            DeadlineUtc = StartedAtUtc.AddSeconds(1),
            StartedAtUtc = StartedAtUtc,
            Execution = new ProgramStepExecutionReference(Guid.NewGuid(), StartedAtUtc, StartedAtUtc.AddSeconds(1)),
        };
        var store = new MemoryStore(CreateRun(pending, plannedStep));
        var status = new ProgramRunStatusCancelReconciliationService(store, new FixedObserver(ProcessIdentityStatus.ExitedOrReplaced), new FakeTimeProvider(StartedAtUtc));

        var result = await status.GetStatusAsync(CreateProject(), store.Current.RunId);

        Assert.Equal(ProgramRunState.Interrupted, result!.State);
        Assert.Equal(ProgramStepState.Deferred, Assert.Single(result.Steps).State);
        Assert.Equal(ExecutionApplicationState.NotApplied, result.Steps[0].ApplicationState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatusAsync_PersistsUnobservableLivenessWithoutTerminalizing ()
    {
        var store = new MemoryStore(CreateRun());
        var status = new ProgramRunStatusCancelReconciliationService(store, new FixedObserver(ProcessIdentityStatus.Unobservable), new FakeTimeProvider(StartedAtUtc));

        var result = await status.GetStatusAsync(CreateProject(), store.Current.RunId);

        Assert.Equal(ProgramRunState.Created, result!.State);
        Assert.Equal(ProcessIdentityStatus.Unobservable, result.SupervisorObservation!.Status);
        Assert.Equal(ProcessIdentityStatus.Matching, result.HostObservation!.Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatusAsync_WhenFixedHostIsLost_InterruptsWithoutCallingTheExecutionPort ()
    {
        var store = new MemoryStore(CreateRun());
        var status = new ProgramRunStatusCancelReconciliationService(store, new FixedObserver(hostStatus: ProcessIdentityStatus.ExitedOrReplaced), new FakeTimeProvider(StartedAtUtc));

        var result = await status.GetStatusAsync(CreateProject(), store.Current.RunId);

        Assert.Equal(ProgramRunState.Interrupted, result!.State);
        Assert.Equal(ProcessIdentityStatus.ExitedOrReplaced, result.HostObservation!.Status);
        Assert.NotNull(store.LastRunTerminal);
        Assert.Equal(ProgramSupervisorConnection.Connected, store.LastRunTerminal.FinalSupervisorSnapshot.Connection);
        Assert.Equal(ProgramSupervisorAvailability.Available, store.LastRunTerminal.FinalSupervisorSnapshot.Availability);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RecoverAsync_WhenTheLiveStepDeadlineExpires_DoesNotStartAnotherStep ()
    {
        var store = new MemoryStore(CreateRun());
        var port = new RecordingPort { StartResult = ProgramStepExecutionPortResult.CommunicationLost };
        var clock = new FakeTimeProvider(StartedAtUtc);
        port.OnRecover = () => clock.Advance(TimeSpan.FromSeconds(2));
        port.RecoveryResult = ProgramStepExecutionRecoveryResult.CommunicationLost;
        var supervisor = CreateSupervisor(store, port, new FixedObserver(), clock);

        var result = await supervisor.StartNextAsync(CreateProject(), store.Current.RunId, ExecutionDeadline.Start(TimeSpan.FromMinutes(1), clock));

        Assert.Equal(ProgramRunState.Interrupted, result!.State);
        Assert.Single(port.Starts);
        Assert.Equal("PROGRAM_COMMUNICATION_RECOVERY_EXPIRED", Assert.Single(result.Steps).ErrorCode);
        var termination = Assert.Single(port.Terminations);
        Assert.Equal(port.Starts[0].Execution.ExecutionId, termination.Execution.ExecutionId);
        Assert.Equal("PROGRAM_STEP_TIMEOUT", termination.ReasonCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RecoverAsync_WhenTheFiniteRecoveryBudgetExpires_UsesTheCommunicationRecoveryReason ()
    {
        var store = new MemoryStore(CreateRun());
        var port = new RecordingPort { StartResult = ProgramStepExecutionPortResult.CommunicationLost, RecoveryResult = ProgramStepExecutionRecoveryResult.CommunicationLost };
        var clock = new FakeTimeProvider(StartedAtUtc);
        port.OnRecover = () => clock.Advance(TimeSpan.FromSeconds(2));
        var supervisor = CreateSupervisor(store, port, new FixedObserver(), clock);

        var result = await supervisor.StartNextAsync(CreateProject(), store.Current.RunId, ExecutionDeadline.Start(TimeSpan.FromSeconds(1), clock));

        Assert.Equal(ProgramRunState.Interrupted, result!.State);
        Assert.Equal("PROGRAM_COMMUNICATION_RECOVERY_EXPIRED", Assert.Single(result.Steps).ErrorCode);
        Assert.Single(port.Starts);
        Assert.Equal(3, port.Recoveries.Count);
        Assert.Equal("PROGRAM_RUN_TIMEOUT", Assert.Single(port.Terminations).ReasonCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RecoverAsync_WhenTheSameExecutionRemainsUnreachable_LeavesTheSameExecutionForALaterDeadlineBoundedRecovery ()
    {
        var store = new MemoryStore(CreateRun());
        var port = new RecordingPort
        {
            StartResult = ProgramStepExecutionPortResult.CommunicationLost,
            RecoveryResult = ProgramStepExecutionRecoveryResult.CommunicationLost,
        };
        var clock = new FakeTimeProvider(StartedAtUtc);
        port.OnRecover = () => clock.Advance(TimeSpan.FromSeconds(2));
        var supervisor = CreateSupervisor(store, port, new FixedObserver(), clock);

        var result = await supervisor.StartNextAsync(CreateProject(), store.Current.RunId, ExecutionDeadline.Start(TimeSpan.FromMinutes(1), clock));

        Assert.Equal(ProgramRunState.Interrupted, result!.State);
        Assert.Equal("PROGRAM_COMMUNICATION_RECOVERY_EXPIRED", Assert.Single(result.Steps).ErrorCode);
        Assert.Single(port.Starts);
        Assert.Equal(3, port.Recoveries.Count);
        Assert.Single(port.Terminations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RecoverAsync_WhenTheSameLogicalExecutionReturnsTypedTerminalFacts_PersistsTheStepWithoutAnotherStart ()
    {
        var store = new MemoryStore(CreateRun());
        var port = new RecordingPort
        {
            StartResult = ProgramStepExecutionPortResult.CommunicationLost,
            RecoveryResult = ProgramStepExecutionRecoveryResult.TerminallyRecovered(
                new ProgramStepExecutionRecoveredTerminal(ProgramStepState.Completed, Verdict.Pass, ExecutionApplicationState.Applied, null)),
        };
        var clock = new FakeTimeProvider(StartedAtUtc);
        var supervisor = CreateSupervisor(store, port, new FixedObserver(), clock);

        var result = await supervisor.StartNextAsync(CreateProject(), store.Current.RunId, ExecutionDeadline.Start(TimeSpan.FromMinutes(1), clock));

        Assert.Equal(ProgramStepState.Completed, Assert.Single(result!.Steps).State);
        Assert.Equal(ExecutionApplicationState.Applied, result.Steps[0].ApplicationState);
        Assert.Single(port.Starts);
        Assert.Single(port.Recoveries);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RecoverAsync_WhenCompletedStepHasASuccessor_StartsOnlyTheNextPersistedStepAndThenCompletesTheRun ()
    {
        var source = CreateRun();
        ProgramRunStepRecord[] steps =
        [
            new ProgramRunPendingStep("ready", 1000).ToRecord(),
            new ProgramRunPendingStep("ready", 1000).ToRecord(),
        ];
        var run = new ProgramRunRecord(
            source.SchemaVersion, source.Version, source.RunId, source.DefinitionDigest, source.DefinitionSnapshotRef,
            source.Project, source.FixedContext, source.Host, source.StartedGeneration, source.CurrentEditorGeneration,
            source.DeadlineUtc, source.StartedAtUtc, source.UpdatedAtUtc, source.State, 0,
            steps, source.ChildExecutionRefs, source.Cancellation, source.TerminalRecordRef);
        var store = new MemoryStore(run);
        var port = new RecordingPort
        {
            StartResult = ProgramStepExecutionPortResult.CommunicationLost,
            RecoveryResult = ProgramStepExecutionRecoveryResult.TerminallyRecovered(
                new ProgramStepExecutionRecoveredTerminal(ProgramStepState.Completed, Verdict.Pass, ExecutionApplicationState.Applied, null)),
        };
        var clock = new FakeTimeProvider(StartedAtUtc);
        var supervisor = CreateSupervisor(store, port, new FixedObserver(), clock);

        var result = await supervisor.StartNextAsync(CreateProject(), run.RunId, ExecutionDeadline.Start(TimeSpan.FromMinutes(1), clock));

        Assert.Equal(ProgramRunState.Completed, result!.State);
        Assert.Equal(2, result.Cursor);
        Assert.All(result.Steps, step => Assert.Equal(ProgramStepState.Completed, step.State));
        Assert.Equal([0, 1], port.Starts.Select(start => start.StepIndex));
        Assert.Equal(2, port.Recoveries.Count);
        Assert.NotNull(result.TerminalRecordRef);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RecoverAsync_WhenTerminationPortIgnoresCancellation_ReturnsAtTheFixedRecoveryDeadlineWithoutAnotherStart ()
    {
        var store = new MemoryStore(CreateRun());
        var clock = new FakeTimeProvider(StartedAtUtc);
        var neverCompletes = new TaskCompletionSource<ProgramStepExecutionTerminationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var port = new RecordingPort
        {
            StartResult = ProgramStepExecutionPortResult.CommunicationLost,
            RecoveryResult = ProgramStepExecutionRecoveryResult.CommunicationLost,
            OnTerminationAsync = (_, _) =>
            {
                terminationStarted.TrySetResult();
                return new ValueTask<ProgramStepExecutionTerminationResult>(neverCompletes.Task);
            },
        };
        port.OnRecover = () => clock.Advance(TimeSpan.FromSeconds(2));
        var supervisor = CreateSupervisor(store, port, new FixedObserver(), clock);

        var pending = supervisor.StartNextAsync(CreateProject(), store.Current.RunId, ExecutionDeadline.Start(TimeSpan.FromMinutes(1), clock)).AsTask();
        await terminationStarted.Task;
        clock.Advance(TimeSpan.FromSeconds(3));
        var result = await pending;

        Assert.Equal(ProgramRunState.Interrupted, result!.State);
        Assert.Equal("PROGRAM_COMMUNICATION_RECOVERY_EXPIRED", Assert.Single(result.Steps).ErrorCode);
        Assert.Single(port.Starts);
        Assert.Single(port.Terminations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RecoverAsync_WhenTerminationCommunicationIsLost_RecoversTheSameExecutionAndFailsItWithTheTimeoutReason ()
    {
        var store = new MemoryStore(CreateRun());
        var clock = new FakeTimeProvider(StartedAtUtc);
        var recoveries = 0;
        var port = new RecordingPort
        {
            StartResult = ProgramStepExecutionPortResult.CommunicationLost,
            TerminationResult = ProgramStepExecutionTerminationResult.CommunicationLost,
            OnRecoveryAsync = (_, _) =>
            {
                recoveries++;
                if (recoveries == 1)
                {
                    clock.Advance(TimeSpan.FromSeconds(2));
                    return ValueTask.FromResult(ProgramStepExecutionRecoveryResult.CommunicationLost);
                }
                return ValueTask.FromResult(ProgramStepExecutionRecoveryResult.TerminallyRecovered(
                    new ProgramStepExecutionRecoveredTerminal(ProgramStepState.Completed, Verdict.Pass, ExecutionApplicationState.PartiallyApplied, null)));
            },
        };
        var supervisor = CreateSupervisor(store, port, new FixedObserver(), clock);

        var result = await supervisor.StartNextAsync(CreateProject(), store.Current.RunId, ExecutionDeadline.Start(TimeSpan.FromMinutes(1), clock));

        Assert.Equal(ProgramRunState.Failed, result!.State);
        var step = Assert.Single(result.Steps);
        Assert.Equal(ProgramStepState.Failed, step.State);
        Assert.Equal("PROGRAM_STEP_TIMEOUT", step.ErrorCode);
        Assert.Equal(ExecutionApplicationState.PartiallyApplied, step.ApplicationState);
        Assert.Single(port.Starts);
        Assert.Single(port.Terminations);
        Assert.Equal(port.Starts[0].Execution.ExecutionId, port.Recoveries.Last().Execution.ExecutionId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void InternalComposition_CreatesEveryNonPublicProgramUseCaseFromExplicitPorts ()
    {
        var store = new MemoryStore(CreateRun());
        var clock = new FakeTimeProvider(StartedAtUtc);
        var persistence = new ProgramRunPersistenceService(store, new FixedGuidGenerator(Guid.NewGuid()), clock);

        var useCases = ProgramInternalUseCaseComposition.Create(
            store, persistence, new RecordingPort(), new RecordingNotification(), new FixedObserver(),
            new FixedGuidGenerator(Guid.NewGuid()), clock, Owner);

        Assert.NotNull(useCases.RunStart);
        Assert.NotNull(useCases.Supervisor);
        Assert.NotNull(useCases.StatusCancel);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartNextAsync_WhenRunDeadlineIsAlreadyExpired_FailsWithoutStartingAPort ()
    {
        var store = new MemoryStore(CreateRun());
        var port = new RecordingPort();
        var clock = new FakeTimeProvider(StartedAtUtc);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1), clock);
        clock.Advance(TimeSpan.FromSeconds(2));
        var supervisor = CreateSupervisor(store, port, new FixedObserver(), clock);

        var result = await supervisor.StartNextAsync(CreateProject(), store.Current.RunId, deadline);

        Assert.Equal(ProgramRunState.Failed, result!.State);
        Assert.Empty(port.Starts);
        Assert.Empty(port.Recoveries);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartNextAsync_WhenTheUnstartedStepDeadlineHasElapsed_FailsThatStepAsNotAppliedAndDoesNotStartThePort ()
    {
        var store = new MemoryStore(CreateRun());
        var port = new RecordingPort();
        var clock = new FakeTimeProvider(StartedAtUtc);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromMinutes(1), clock);
        store.OnCompareExchange = count =>
        {
            if (count == 2)
            {
                clock.Advance(TimeSpan.FromSeconds(2));
            }
        };
        var supervisor = CreateSupervisor(store, port, new FixedObserver(), clock);
        var run = CreateRun(store.Current, store.Current.Steps[0] with { TimeoutMilliseconds = 1 });
        store.Replace(run);

        var result = await supervisor.StartNextAsync(CreateProject(), run.RunId, deadline);

        Assert.Equal(ProgramRunState.Failed, result!.State);
        var step = Assert.Single(result.Steps);
        Assert.Equal(ProgramStepState.Failed, step.State);
        Assert.Equal(ExecutionApplicationState.NotApplied, step.ApplicationState);
        Assert.Equal("PROGRAM_STEP_TIMEOUT", step.ErrorCode);
        Assert.Empty(port.Starts);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartNextAsync_WhenRunAndUnstartedStepDeadlinesExpireTogether_PrioritizesTheRunAndRetainsDeferredPlanningAuditFacts ()
    {
        var store = new MemoryStore(CreateRun());
        var port = new RecordingPort();
        var clock = new FakeTimeProvider(StartedAtUtc);
        store.OnCompareExchange = count =>
        {
            if (count == 2)
            {
                clock.Advance(TimeSpan.FromSeconds(2));
            }
        };
        var run = CreateRun(store.Current, store.Current.Steps[0] with { TimeoutMilliseconds = 1 });
        store.Replace(run);
        var supervisor = CreateSupervisor(store, port, new FixedObserver(), clock);

        var result = await supervisor.StartNextAsync(CreateProject(), run.RunId, ExecutionDeadline.Start(TimeSpan.FromMilliseconds(1), clock));

        Assert.Equal(ProgramRunState.Failed, result!.State);
        var step = Assert.Single(result.Steps);
        Assert.Equal(ProgramStepState.Deferred, step.State);
        Assert.Equal(ExecutionApplicationState.NotApplied, step.ApplicationState);
        Assert.Equal(StartedAtUtc, step.PlanningStartedAtUtc);
        Assert.Equal(StartedAtUtc.AddMilliseconds(1), step.DeadlineUtc);
        Assert.Equal("PROGRAM_RUN_TIMEOUT", store.LastRunTerminal!.ReasonCode);
        Assert.Empty(port.Starts);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RecoverAsync_PassesTheFixedStepDeadlineAndRemainingBudgetToTheClosedPort ()
    {
        var store = new MemoryStore(CreateRun());
        var port = new RecordingPort { StartResult = ProgramStepExecutionPortResult.CommunicationLost };
        var clock = new FakeTimeProvider(StartedAtUtc);
        var run = CreateRun(store.Current, store.Current.Steps[0] with { TimeoutMilliseconds = 500 });
        store.Replace(run);
        var supervisor = CreateSupervisor(store, port, new FixedObserver(), clock);

        await supervisor.StartNextAsync(CreateProject(), run.RunId, ExecutionDeadline.Start(TimeSpan.FromMinutes(1), clock));

        var recovery = Assert.Single(port.Recoveries);
        Assert.Equal(port.Starts[0].Execution.DeadlineUtc, recovery.Deadline.UtcDeadline);
        Assert.InRange(recovery.RemainingTimeout, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartNextAsync_UsesTheLiveMonotonicDeadlineRatherThanThePersistedUtcAuditProjection ()
    {
        var source = CreateRun();
        var auditedPastDeadline = new ProgramRunRecord(
            source.SchemaVersion, source.Version, source.RunId, source.DefinitionDigest, source.DefinitionSnapshotRef,
            source.Project, source.FixedContext, source.Host, source.StartedGeneration, source.CurrentEditorGeneration,
            StartedAtUtc.AddSeconds(1), source.StartedAtUtc, source.UpdatedAtUtc, source.State, source.Cursor,
            source.Steps, source.ChildExecutionRefs, source.Cancellation, source.TerminalRecordRef);
        var store = new MemoryStore(auditedPastDeadline);
        var port = new RecordingPort();
        var clock = new FakeTimeProvider(StartedAtUtc.AddMinutes(1));
        var supervisor = CreateSupervisor(store, port, new FixedObserver(), clock);

        var result = await supervisor.StartNextAsync(CreateProject(), source.RunId, ExecutionDeadline.Start(TimeSpan.FromMinutes(1), clock));

        Assert.Equal(ProgramRunState.Running, result!.State);
        Assert.Single(port.Starts);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartNextAsync_WhenTheFirstLostObservationCannotBePersisted_ReobservesBeforeDecidingLiveness ()
    {
        var store = new MemoryStore(CreateRun()) { RejectNextCompareExchange = true };
        var port = new RecordingPort();
        var observer = new SequenceObserver([ProcessIdentityStatus.ExitedOrReplaced, ProcessIdentityStatus.Matching]);
        var supervisor = CreateSupervisor(store, port, observer, new FakeTimeProvider(StartedAtUtc));

        var result = await supervisor.StartNextAsync(CreateProject(), store.Current.RunId, ExecutionDeadline.Start(TimeSpan.FromMinutes(1), new FakeTimeProvider(StartedAtUtc)));

        Assert.Equal(ProgramRunState.Running, result!.State);
        Assert.Single(port.Starts);
        Assert.Equal(ProcessIdentityStatus.Matching, result.SupervisorObservation!.Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartNextAsync_WhenObservationCasReturnsATerminalRun_ReturnsItWithoutStartingAPort ()
    {
        var store = new MemoryStore(CreateRun());
        var port = new RecordingPort();
        var clock = new FakeTimeProvider(StartedAtUtc);
        var observer = new CountingObserver();
        store.OnCompareExchange = count =>
        {
            if (count == 1)
            {
                store.Replace(CreateTerminalRun(store.Current));
            }
        };
        var supervisor = CreateSupervisor(store, port, observer, clock);

        var result = await supervisor.StartNextAsync(CreateProject(), store.Current.RunId, ExecutionDeadline.Start(TimeSpan.FromMinutes(1), clock));

        Assert.Equal(ProgramRunState.Failed, result!.State);
        Assert.Equal("PROGRAM_RUN_TIMEOUT", result.TerminalReasonCode);
        Assert.Equal(CreateArtifact("programRunTerminalRecord", "terminal.json"), result.TerminalRecordRef);
        Assert.Single(result.Steps);
        Assert.Equal(ProgramStepState.Failed, result.Steps[0].State);
        Assert.NotNull(result.Steps[0].ResultRef);
        Assert.Empty(port.Starts);
        Assert.Equal(2, observer.ObservationCount);
        Assert.Equal(1, store.Operations.Count(static operation => operation == "compareExchange"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CancelAsync_WhenOneStatusWriterWinsTheLivenessCas_RetriesAndPreservesCancellationWithoutStartingAPort ()
    {
        var store = new MemoryStore(CreateRun()) { RejectNextCompareExchange = true };
        var status = new ProgramRunStatusCancelReconciliationService(store, new FixedObserver(), new FakeTimeProvider(StartedAtUtc));

        var result = await status.CancelAsync(CreateProject(), store.Current.RunId, "USER_CANCELLED");

        Assert.Equal(ProgramRunState.Cancelled, result!.State);
        Assert.True(result.Cancellation.Requested);
        Assert.Equal("USER_CANCELLED", result.Cancellation.ReasonCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StatusAndCancelAsync_WhenTheirObservationCasConflicts_ConvergeOnOneCancelledTerminalFact ()
    {
        var store = new MemoryStore(CreateRun()) { SynchronizeFirstTwoCompareExchanges = true };
        var service = new ProgramRunStatusCancelReconciliationService(store, new FixedObserver(), new FakeTimeProvider(StartedAtUtc));

        var status = service.GetStatusAsync(CreateProject(), store.Current.RunId).AsTask();
        var cancel = service.CancelAsync(CreateProject(), store.Current.RunId, "USER_CANCELLED").AsTask();
        await Task.WhenAll(status, cancel);
        var final = await service.GetStatusAsync(CreateProject(), store.Current.RunId);

        Assert.NotNull(status.Result);
        Assert.NotNull(cancel.Result);
        Assert.Equal(ProgramRunState.Cancelled, final!.State);
        Assert.True(final.Cancellation.Requested);
        Assert.Equal("USER_CANCELLED", final.Cancellation.ReasonCode);
        Assert.NotNull(final.TerminalRecordRef);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CancelAsync_DoesNotPublishATerminalRecordBeforeTheAttachedExecutionIsRecovered ()
    {
        var source = CreateRun();
        var started = source.Steps[0] with
        {
            State = ProgramStepState.Running,
            PlanningStartedAtUtc = StartedAtUtc,
            DeadlineUtc = StartedAtUtc.AddSeconds(1),
            StartedAtUtc = StartedAtUtc,
            Execution = new ProgramStepExecutionReference(Guid.NewGuid(), StartedAtUtc, StartedAtUtc.AddSeconds(1)),
            ExecutionPortInvoked = true,
        };
        var store = new MemoryStore(CreateRun(source, started)) { RejectRunTerminalPublications = true };
        var service = new ProgramRunStatusCancelReconciliationService(store, new FixedObserver(), new FakeTimeProvider(StartedAtUtc));

        var result = await service.CancelAsync(CreateProject(), store.Current.RunId, "USER_CANCELLED");

        Assert.Equal(ProgramRunState.Cancelling, result!.State);
        Assert.True(store.Current.Cancellation.Requested);
        Assert.Null(store.Current.TerminalRecordRef);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RecoverAsync_WhenCancellationWasRequested_RecoversAndCancelsOnlyThePersistedExecution ()
    {
        var source = CreateRun();
        var execution = new ProgramStepExecutionReference(Guid.NewGuid(), StartedAtUtc, StartedAtUtc.AddMinutes(1));
        var running = source.Steps[0] with
        {
            State = ProgramStepState.Running,
            PlanningStartedAtUtc = StartedAtUtc,
            DeadlineUtc = execution.DeadlineUtc,
            StartedAtUtc = execution.StartedAtUtc,
            Execution = execution,
            ExecutionPortInvoked = true,
        };
        var requestedAtUtc = StartedAtUtc.AddSeconds(1);
        var cancelled = new ProgramRunRecord(
            source.SchemaVersion, source.Version + 1, source.RunId, source.DefinitionDigest, source.DefinitionSnapshotRef,
            source.Project, source.FixedContext, source.Host, source.StartedGeneration, source.CurrentEditorGeneration,
            source.DeadlineUtc, source.StartedAtUtc, requestedAtUtc, ProgramRunState.Cancelling, source.Cursor,
            [running], source.ChildExecutionRefs, source.Cancellation.Request(requestedAtUtc, "USER_CANCELLED"), source.TerminalRecordRef);
        var store = new MemoryStore(cancelled);
        var port = new RecordingPort
        {
            RecoveryResult = ProgramStepExecutionRecoveryResult.TerminallyRecovered(
                new ProgramStepExecutionRecoveredTerminal(ProgramStepState.Completed, Verdict.Pass, ExecutionApplicationState.Applied, null)),
        };
        var clock = new FakeTimeProvider(StartedAtUtc);
        var supervisor = CreateSupervisor(store, port, new FixedObserver(), clock);

        var result = await supervisor.RecoverAsync(
            CreateProject(), cancelled.RunId, ExecutionDeadline.Start(TimeSpan.FromMinutes(1), clock));

        Assert.Equal(ProgramRunState.Cancelled, result!.State);
        Assert.Equal(ProgramStepState.Cancelled, Assert.Single(result.Steps).State);
        Assert.Equal("PROGRAM_RUN_CANCELLED", result.Steps[0].ErrorCode);
        Assert.Equal(execution.ExecutionId, Assert.Single(port.Terminations).Execution.ExecutionId);
        Assert.Single(port.Recoveries);
        Assert.Empty(port.Starts);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatusAsync_WhenAnInvokedStepTerminalPublicationConflicts_RetriesTheOuterTerminalizationWithoutStartingAPort ()
    {
        var pending = CreateRun();
        var invoked = pending.Steps[0] with
        {
            State = ProgramStepState.Planning,
            PlanningStartedAtUtc = StartedAtUtc,
            DeadlineUtc = StartedAtUtc.AddSeconds(1),
            StartedAtUtc = StartedAtUtc,
            Execution = new ProgramStepExecutionReference(Guid.NewGuid(), StartedAtUtc, StartedAtUtc.AddSeconds(1)),
            ExecutionPortInvoked = true,
        };
        var store = new MemoryStore(CreateRun(pending, invoked)) { RejectNextStepTerminalPublication = true };
        var status = new ProgramRunStatusCancelReconciliationService(store, new FixedObserver(ProcessIdentityStatus.ExitedOrReplaced), new FakeTimeProvider(StartedAtUtc));

        var result = await status.GetStatusAsync(CreateProject(), store.Current.RunId);

        Assert.Equal(ProgramRunState.Interrupted, result!.State);
        Assert.Equal(2, store.StepTerminalPublicationAttempts);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PublishRecoveredTerminalAsync_WhenTheFirstCasConflicts_ReloadsAndPublishesTheSameExecutionTerminal ()
    {
        var pending = CreateRun();
        var execution = new ProgramStepExecutionReference(Guid.NewGuid(), StartedAtUtc, StartedAtUtc.AddSeconds(1));
        var invoked = pending.Steps[0] with
        {
            State = ProgramStepState.Planning,
            PlanningStartedAtUtc = StartedAtUtc,
            DeadlineUtc = StartedAtUtc.AddSeconds(1),
            StartedAtUtc = StartedAtUtc,
            Execution = execution,
            ExecutionPortInvoked = true,
        };
        var store = new MemoryStore(CreateRun(pending, invoked)) { RejectNextStepTerminalPublication = true };
        var terminalizer = new ProgramRunTerminalizer(new FakeTimeProvider(StartedAtUtc));

        var result = await terminalizer.PublishRecoveredTerminalAsync(store, store.Current, 0,
            new ProgramStepExecutionRecoveredTerminal(ProgramStepState.Completed, Verdict.Pass, ExecutionApplicationState.Applied, null),
            CancellationToken.None);

        Assert.Equal(ProgramStepState.Completed, Assert.Single(result.Steps).State);
        Assert.Equal(2, store.StepTerminalPublicationAttempts);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task NotificationFailure_AfterRegistration_TerminalizesTheRunWithoutStartingAStep ()
    {
        var store = new MemoryStore(CreateRun());
        var clock = new FakeTimeProvider(StartedAtUtc);
        var supervisor = CreateSupervisor(store, new RecordingPort(), new FixedObserver(), clock);
        var persistence = new ProgramRunPersistenceService(store, new FixedGuidGenerator(Guid.NewGuid()), clock);
        var service = new ProgramRunStartService(persistence, new RecordingNotification(), supervisor, store, clock);

        var result = await service.HandleNotificationFailureAsync(CreateProject(), store.Current);

        Assert.Equal(ProgramRunState.Failed, result.State);
        Assert.Equal(ProgramStepState.Deferred, Assert.Single(result.Steps).State);
        Assert.Equal(ExecutionApplicationState.NotApplied, result.Steps[0].ApplicationState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartAsync_WhenTheRegisteredRunNotificationFails_DoesNotAdmitOrInvokeAStep ()
    {
        var store = new MemoryStore(CreateRun()) { AcceptCreate = true };
        var clock = new FakeTimeProvider(StartedAtUtc);
        var port = new RecordingPort();
        var supervisor = CreateSupervisor(store, port, new FixedObserver(), clock);
        var persistence = new ProgramRunPersistenceService(store, new FixedGuidGenerator(Guid.Parse("00000000-0000-0000-0000-000000000222")), clock);
        var service = new ProgramRunStartService(persistence, new FailingNotification(), supervisor, store, clock);

        var result = await service.StartAsync(CreateRegistrationRequest(), ExecutionDeadline.Start(TimeSpan.FromMinutes(1), clock));

        Assert.Equal(ProgramRunState.Failed, result.State);
        Assert.Equal(ProgramStepState.Deferred, result.Steps[0].State);
        Assert.False(result.Steps[0].ExecutionPortInvoked);
        Assert.Equal("PROGRAM_START_EVENT_WRITE_FAILED", store.LastRunTerminal!.ReasonCode);
        Assert.Empty(port.Starts);
    }

    [Theory]
    [InlineData(true, ProgramRunState.Interrupted, null)]
    [InlineData(false, ProgramRunState.Created, null)]
    [Trait("Size", "Small")]
    public async Task GetStatusAsync_OnlyConfirmedOwnerLossTerminalizesTheRun (
        bool ownerLost,
        ProgramRunState expectedState,
        string? expectedErrorCode)
    {
        var store = new MemoryStore(CreateRun());
        var ownerStatus = ownerLost ? ProcessIdentityStatus.ExitedOrReplaced : ProcessIdentityStatus.Unobservable;
        var status = new ProgramRunStatusCancelReconciliationService(store, new FixedObserver(ownerStatus), new FakeTimeProvider(StartedAtUtc));

        var result = await status.GetStatusAsync(CreateProject(), store.Current.RunId);

        Assert.Equal(expectedState, result!.State);
        if (expectedErrorCode is not null)
        {
            Assert.Equal(expectedErrorCode, Assert.Single(result.Steps).ErrorCode);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Size", "Small")]
    public async Task StatusOrCancelAsync_WhenOwnerIsLostAndTheSameLifecycleTerminalIsAvailable_ProjectsItBeforeInterrupting (bool cancel)
    {
        var run = CreateRunningRefreshRun(out var activeReference);
        var terminalReference = CreateTerminalRefreshReference(activeReference.Id);
        var terminal = CreateRefreshTerminal(run, activeReference.Id, run.Host);
        var resolver = new RecordingLifecycleExecutionReconnectResolver(
            new LifecycleExecutionReconnectResolution.Terminal(terminalReference, terminal));
        var store = new MemoryStore(run);
        var service = new ProgramRunStatusCancelReconciliationService(
            store,
            new FixedObserver(ProcessIdentityStatus.ExitedOrReplaced),
            new FakeTimeProvider(StartedAtUtc),
            resolver);

        var result = cancel
            ? await service.CancelAsync(CreateProject(), run.RunId, "USER_CANCELLED")
            : await service.GetStatusAsync(CreateProject(), run.RunId);

        Assert.Equal(ProgramRunState.Interrupted, result!.State);
        var step = Assert.Single(result.Steps);
        Assert.Equal(ProgramStepState.Failed, step.State);
        Assert.Equal(terminalReference, step.LifecycleExecutionRef);
        Assert.Equal(ExecutionApplicationState.Applied, step.ApplicationState);
        Assert.Single(resolver.Invocations);
        Assert.False(result.Cancellation.Requested);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatusAsync_WhenLifecycleTerminalDoesNotMatchTheFixedHost_LeavesTheActiveReferenceUnknownAndInterrupts ()
    {
        var run = CreateRunningRefreshRun(out var activeReference);
        var terminalReference = CreateTerminalRefreshReference(activeReference.Id);
        var mismatchedHost = new LifecycleExecutionHostRegistration(
            run.Host.Process,
            Guid.NewGuid(),
            run.Host.FirstEndpointRegistrationGenerationId,
            run.Host.CurrentEndpointRegistrationGenerationId);
        var resolver = new RecordingLifecycleExecutionReconnectResolver(
            new LifecycleExecutionReconnectResolution.Terminal(
                terminalReference,
                CreateRefreshTerminal(run, activeReference.Id, mismatchedHost)));
        var store = new MemoryStore(run);
        var service = new ProgramRunStatusCancelReconciliationService(
            store,
            new FixedObserver(hostStatus: ProcessIdentityStatus.ExitedOrReplaced),
            new FakeTimeProvider(StartedAtUtc),
            resolver);

        var result = await service.GetStatusAsync(CreateProject(), run.RunId);

        Assert.Equal(ProgramRunState.Interrupted, result!.State);
        var step = Assert.Single(result.Steps);
        Assert.Equal(ProgramStepState.Interrupted, step.State);
        Assert.Equal(activeReference, step.LifecycleExecutionRef);
        Assert.Equal(ExecutionApplicationState.Unknown, step.ApplicationState);
        Assert.Single(resolver.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CancelAsync_IsIdempotentAndDoesNotCallAnExecutionPort ()
    {
        var store = new MemoryStore(CreateRun());
        var status = new ProgramRunStatusCancelReconciliationService(store, new FixedObserver(), new FakeTimeProvider(StartedAtUtc));

        var first = await status.CancelAsync(CreateProject(), store.Current.RunId, "USER_CANCELLED");
        var second = await status.CancelAsync(CreateProject(), store.Current.RunId, "ignored");

        Assert.Equal(ProgramRunState.Cancelled, first!.State);
        Assert.Same(first, second);
        Assert.True(first.Cancellation.Requested);
        Assert.Equal("USER_CANCELLED", first.Cancellation.ReasonCode);
        Assert.Equal(first.Cancellation, second!.Cancellation);
    }

    private static ProgramAttachedSupervisor CreateSupervisor (MemoryStore store, RecordingPort port, IProcessIdentityObserver observer, TimeProvider clock) => new(
        store, port, observer, new FixedGuidGenerator(Guid.Parse("00000000-0000-0000-0000-000000000111")), clock, Owner);

    private static ProgramRunRecord CreateRun ()
    {
        var definitionRef = CreateArtifact("programDefinitionSnapshot", "definition.json");
        return new ProgramRunRecord(
            ProgramRunRecord.CurrentSchemaVersion, 0, Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Sha256Digest.Parse(new string('a', 64)), definitionRef, CreateProjectIdentity(), CreateFixedContext(),
            new LifecycleExecutionHostRegistration(new ProcessIdentity(200, 20), Guid.Parse("00000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000003")),
            new UnityEditorGenerationSnapshot(0, 0, 0, 0), null, StartedAtUtc.AddMinutes(10), StartedAtUtc, StartedAtUtc,
            ProgramRunState.Created, 0,
            [new ProgramRunPendingStep("ready", 1000).ToRecord()], [], ProgramCancellationRecord.None, null);
    }

    private static ProgramRunRecord CreateRunningRefreshRun (out ActiveExecutionRef lifecycleReference)
    {
        var source = CreateRun();
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        lifecycleReference = new ActiveExecutionRef(
            definition.ExecutionKind,
            Guid.Parse("00000000-0000-0000-0000-000000000333"),
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new ExecutionState(TextVocabulary.GetText(LifecycleExecutionState.Registered)),
            new ExecutionStatusLocator(".ucli/local/lifecycle-executions/refresh/00000000000000000000000000000333/execution.json"));
        var execution = new ProgramStepExecutionReference(
            Guid.Parse("00000000-0000-0000-0000-000000000444"), StartedAtUtc, StartedAtUtc.AddMinutes(1));
        var step = new ProgramRunStepRecord(
            "refresh", 1000, ProgramStepState.Running, null, StartedAtUtc, execution.DeadlineUtc,
            source.StartedGeneration, null, ExecutionApplicationState.NotApplied, null, [], lifecycleReference,
            null, null, null, null, [], null, execution.StartedAtUtc, null)
        {
            Execution = execution,
            ExecutionPortInvoked = true,
        };
        return new ProgramRunRecord(
            source.SchemaVersion, source.Version, source.RunId, source.DefinitionDigest, source.DefinitionSnapshotRef,
            source.Project, source.FixedContext, source.Host, source.StartedGeneration, source.StartedGeneration,
            source.DeadlineUtc, source.StartedAtUtc, source.UpdatedAtUtc, ProgramRunState.WaitingForRuntime, source.Cursor,
            [step], source.ChildExecutionRefs, source.Cancellation, null);
    }

    private static TerminalExecutionRef CreateTerminalRefreshReference (Guid executionId)
    {
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        return new TerminalExecutionRef(
            definition.ExecutionKind,
            executionId,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new ExecutionState(TextVocabulary.GetText(LifecycleExecutionState.Completed)),
            null,
            CreateArtifact("lifecycleExecutionTerminalRecord", "lifecycle-terminal.json"));
    }

    private static RefreshLifecycleExecutionTerminalRecord CreateRefreshTerminal (
        ProgramRunRecord run,
        Guid executionId,
        LifecycleExecutionHostRegistration host) => new(
        executionId,
        LifecycleExecutionDefinitionDigest.Calculate(new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh)),
        run.Project,
        host,
        run.StartedGeneration,
        run.StartedGeneration,
        StartedAtUtc.AddMinutes(2),
        StartedAtUtc,
        StartedAtUtc.AddSeconds(1),
        LifecycleExecutionTerminalReason.ActionFailed,
        ExecutionApplicationState.Applied,
        null,
        null,
        []);

    private static ProgramRunRecord CreateRun (ProgramRunRecord source, ProgramRunStepRecord step) => new(
        source.SchemaVersion, source.Version, source.RunId, source.DefinitionDigest, source.DefinitionSnapshotRef,
        source.Project, source.FixedContext, source.Host, source.StartedGeneration, source.CurrentEditorGeneration,
        source.DeadlineUtc, source.StartedAtUtc, source.UpdatedAtUtc, ProgramRunState.Created, 0,
        [step], source.ChildExecutionRefs, source.Cancellation, source.TerminalRecordRef);

    private static ProgramRunRecord ReplaceStep (ProgramRunRecord source, ProgramRunStepRecord step) => new(
        source.SchemaVersion, source.Version + 1, source.RunId, source.DefinitionDigest, source.DefinitionSnapshotRef,
        source.Project, source.FixedContext, source.Host, source.StartedGeneration, source.CurrentEditorGeneration,
        source.DeadlineUtc, source.StartedAtUtc, source.UpdatedAtUtc, source.State, source.Cursor,
        [step], source.ChildExecutionRefs, source.Cancellation, source.TerminalRecordRef)
    {
        SupervisorObservation = source.SupervisorObservation,
        HostObservation = source.HostObservation,
        TerminalReasonCode = source.TerminalReasonCode,
    };

    private static ProgramRunRecord CreateTerminalRun (ProgramRunRecord source) => new(
        source.SchemaVersion, source.Version + 1, source.RunId, source.DefinitionDigest, source.DefinitionSnapshotRef,
        source.Project, source.FixedContext, source.Host, source.StartedGeneration, source.CurrentEditorGeneration,
        source.DeadlineUtc, source.StartedAtUtc, source.UpdatedAtUtc, ProgramRunState.Failed, source.Cursor,
        [new ProgramRunStepRecord("ready", 1000, ProgramStepState.Failed, null, StartedAtUtc, StartedAtUtc.AddSeconds(1), null, null,
            ExecutionApplicationState.NotApplied, null, [], null, null, null, CreateArtifact("programStepTerminalRecord", "step-terminal.json"),
            null, [], "PROGRAM_RUN_TIMEOUT", null, StartedAtUtc.AddSeconds(1))], source.ChildExecutionRefs, source.Cancellation,
        CreateArtifact("programRunTerminalRecord", "terminal.json"))
    {
        TerminalReasonCode = "PROGRAM_RUN_TIMEOUT",
    };

    private static ProgramRunRegistrationRequest CreateRegistrationRequest ()
    {
        using var document = JsonDocument.Parse("{\"steps\":[{\"command\":\"ready\",\"timeoutMilliseconds\":1000}]}");
        var definition = new ResolvedProgramDefinition(
            new ProgramDefinition([new ReadyProgramStep(1000)], document.RootElement.Clone()), [],
            new ProgramSourceManifest(
                Sha256Digest.Parse("ad9deb8f7f2628012c4f15ffd29a79892ddceaa9237b530951f5b8aad33b60be"),
                ProgramRootSource.Stdin, null, null,
                Sha256Digest.Parse("7122109bf0b4b7b10dab6e76b8f9b57d7532d1f0e010ae0215366d78b3e23e28"), []),
            Sha256Digest.Parse("14c934ffaac9d7cfce1bcda1de4d74cfbc14d35d8f3eae8d119dfb2e84c5c629"));
        return new ProgramRunRegistrationRequest(
            CreateProject(), CreateProjectIdentity(), definition, CreateFixedContext(),
            new LifecycleExecutionHostRegistration(new ProcessIdentity(200, 20), Guid.Parse("00000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000003")),
            new UnityEditorGenerationSnapshot(0, 0, 0, 0), null, StartedAtUtc.AddMinutes(10), [new ProgramRunPendingStep("ready", 1000)]);
    }

    private static ProgramRunFixedContext CreateFixedContext ()
    {
        var commandTimeouts = new Dictionary<string, int> { ["ready"] = 1000 };
        return new ProgramRunFixedContext(
            new ProgramEffectiveAuthorizationSnapshot(false, false, IpcProgramEffectiveAuthorizationSnapshot.ComputeDigest(false, false).ToString(), StartedAtUtc),
            new ProgramEffectiveConfigurationSnapshot(1, OperationPolicy.Safe, PlanTokenMode.Optional, ReadIndexMode.RequireFresh, [], 1000, commandTimeouts,
            IpcProgramEffectiveConfigurationSnapshot.ComputeDigest(1, "safe", "optional", "requireFresh", [], 1000, commandTimeouts), StartedAtUtc),
            new ProgramExecutionModeSnapshot("auto", "daemon"),
            new ProgramAttachedSupervisorSnapshot(Guid.Parse("00000000-0000-0000-0000-000000000010"), Guid.Parse("00000000-0000-0000-0000-000000000011"), Owner, ProgramSupervisorConnection.Connected, ProgramSupervisorAvailability.Available, StartedAtUtc));
    }

    private static ResolvedUnityProjectContext CreateProject () => ResolvedUnityProjectContext.Create(
        AbsolutePath.Parse(ProjectPathTestValues.WorkspaceUnityProject), AbsolutePath.Parse(ProjectPathTestValues.WorkspaceRoot), new ProjectFingerprint(new string('c', 64)), UnityProjectPathSource.CurrentDirectory, null, "6000.1.0f1");

    private static UnityProjectIdentity CreateProjectIdentity () => new("/project", new ProjectFingerprint(new string('c', 64)), "6000.1.0f1");

    private static ArtifactRef CreateArtifact (string kind, string path) => new PathArtifactRef(
        new ArtifactKind(kind), new ArtifactMediaType("application/json"), new ArtifactPath(path), Sha256Digest.Parse(new string('d', 64)), 1, StartedAtUtc);

    private sealed class FixedGuidGenerator (Guid value) : IGuidGenerator
    {
        public Guid Generate () => value;
    }

    private sealed class FixedObserver (ProcessIdentityStatus ownerStatus = ProcessIdentityStatus.Matching, ProcessIdentityStatus hostStatus = ProcessIdentityStatus.Matching) : IProcessIdentityObserver
    {
        public ProcessIdentityStatus Observe (ProcessIdentity process) => process == Owner ? ownerStatus : hostStatus;
    }

    private sealed class SequenceObserver (IReadOnlyList<ProcessIdentityStatus> ownerStatuses) : IProcessIdentityObserver
    {
        private int ownerObservationIndex;

        public ProcessIdentityStatus Observe (ProcessIdentity process)
        {
            if (process != Owner)
            {
                return ProcessIdentityStatus.Matching;
            }
            var index = Math.Min(Interlocked.Increment(ref ownerObservationIndex) - 1, ownerStatuses.Count - 1);
            return ownerStatuses[index];
        }
    }

    private sealed class CountingObserver : IProcessIdentityObserver
    {
        public int ObservationCount { get; private set; }

        public ProcessIdentityStatus Observe (ProcessIdentity process)
        {
            ObservationCount++;
            return ProcessIdentityStatus.Matching;
        }
    }

    private sealed class RecordingPort : IProgramStepExecutionPort
    {
        public List<string> Operations { get; } = [];
        public List<ProgramStepExecutionStart> Starts { get; } = [];
        public List<ProgramStepExecutionRecovery> Recoveries { get; } = [];
        public List<ProgramStepExecutionTermination> Terminations { get; } = [];
        public ProgramStepExecutionPortResult StartResult { get; init; } = ProgramStepExecutionPortResult.Started;
        public Func<ProgramStepExecutionStart, CancellationToken, ValueTask<ProgramStepExecutionPortResult>>? OnStartAsync { get; init; }
        public ProgramStepExecutionRecoveryResult RecoveryResult { get; set; } = ProgramStepExecutionRecoveryResult.Recovered;
        public Action? OnRecover { get; set; }
        public Func<ProgramStepExecutionRecovery, CancellationToken, ValueTask<ProgramStepExecutionRecoveryResult>>? OnRecoveryAsync { get; set; }
        public Func<ProgramStepExecutionTermination, CancellationToken, ValueTask<ProgramStepExecutionTerminationResult>>? OnTerminationAsync { get; set; }
        public ProgramStepExecutionTerminationResult TerminationResult { get; set; } = ProgramStepExecutionTerminationResult.Requested;

        public ValueTask<ProgramStepExecutionPortResult> StartAsync (ProgramStepExecutionStart start, CancellationToken cancellationToken = default)
        {
            Operations.Add("start");
            Starts.Add(start);
            if (OnStartAsync is not null)
            {
                return OnStartAsync(start, cancellationToken);
            }
            return ValueTask.FromResult(StartResult);
        }

        public ValueTask<ProgramStepExecutionRecoveryResult> RecoverAsync (ProgramStepExecutionRecovery recovery, CancellationToken cancellationToken = default)
        {
            Operations.Add("recover");
            Recoveries.Add(recovery);
            if (OnRecoveryAsync is not null)
            {
                return OnRecoveryAsync(recovery, cancellationToken);
            }
            OnRecover?.Invoke();
            return ValueTask.FromResult(RecoveryResult);
        }

        public ValueTask<ProgramStepExecutionTerminationResult> RequestTerminationAsync (ProgramStepExecutionTermination termination, CancellationToken cancellationToken = default)
        {
            Operations.Add("terminate");
            Terminations.Add(termination);
            if (OnTerminationAsync is not null)
            {
                return OnTerminationAsync(termination, cancellationToken);
            }
            return ValueTask.FromResult(TerminationResult);
        }
    }

    private sealed class RecordingNotification : IProgramRunStartNotificationPort
    {
        public ValueTask NotifyAsync (ProgramRunRecord run, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    private sealed class FailingNotification : IProgramRunStartNotificationPort
    {
        public ValueTask NotifyAsync (ProgramRunRecord run, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new IOException("Notification write failed."));
    }

    private sealed class MemoryStore : IProgramRunStoreFactory, IProgramRunStore
    {
        public MemoryStore (ProgramRunRecord current)
        {
            Current = current;
        }

        public ProgramRunRecord Current { get; private set; }

        public void Replace (ProgramRunRecord run) => Current = run;
        public List<string> Operations { get; } = [];
        public bool RejectNextCompareExchange { get; set; }
        public bool AcceptCreate { get; init; }
        public bool SynchronizeFirstTwoCompareExchanges { get; init; }
        public bool RejectRunTerminalPublications { get; init; }
        public bool RejectNextStepTerminalPublication { get; set; }
        public int StepTerminalPublicationAttempts { get; private set; }
        public Action<int>? OnCompareExchange { get; set; }
        private ProgramDefinitionSnapshot? publishedDefinition;
        public ProgramRunTerminalRecord? LastRunTerminal { get; private set; }
        private readonly TaskCompletionSource firstTwoCompareExchanges = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object compareExchangeGate = new();
        private int synchronizedCompareExchangeCount;
        private int compareExchangeCount;

        public IProgramRunStore ForProject (ResolvedUnityProjectContext project) => this;
        public ValueTask<ArtifactRef> PublishDefinitionSnapshotAsync (Guid runId, ProgramDefinitionSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            publishedDefinition = snapshot;
            return ValueTask.FromResult(CreateArtifact("programDefinitionSnapshot", "definition.json"));
        }

        public ValueTask<ProgramRunStoreCreateResult> CreateAsync (ProgramRunRecord run, CancellationToken cancellationToken = default)
        {
            if (!AcceptCreate)
            {
                throw new NotSupportedException();
            }
            Current = run;
            return ValueTask.FromResult(new ProgramRunStoreCreateResult(true, Current));
        }
        public ValueTask<ProgramRunRecord?> ReadAsync (Guid runId, CancellationToken cancellationToken = default) => ValueTask.FromResult<ProgramRunRecord?>(runId == Current.RunId ? Current : null);
        public ValueTask<ProgramRunStoredDefinition?> ReadDefinitionAsync (Guid runId, CancellationToken cancellationToken = default) => ValueTask.FromResult<ProgramRunStoredDefinition?>(new ProgramRunStoredDefinition(Current, publishedDefinition?.RestoreFixedDefinition() ?? CreateDefinition()));

        public async ValueTask<ProgramRunStoreCompareExchangeResult> CompareExchangeAsync (ProgramRunRecord expected, ProgramRunRecord replacement, CancellationToken cancellationToken = default)
        {
            Operations.Add("compareExchange");
            OnCompareExchange?.Invoke(Interlocked.Increment(ref compareExchangeCount));
            if (SynchronizeFirstTwoCompareExchanges && Interlocked.Increment(ref synchronizedCompareExchangeCount) <= 2)
            {
                if (Volatile.Read(ref synchronizedCompareExchangeCount) == 2)
                {
                    firstTwoCompareExchanges.TrySetResult();
                }
                await firstTwoCompareExchanges.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            lock (compareExchangeGate)
            {
                if (RejectNextCompareExchange)
                {
                    RejectNextCompareExchange = false;
                    Current = new ProgramRunRecord(
                        Current.SchemaVersion, Current.Version + 1, Current.RunId, Current.DefinitionDigest, Current.DefinitionSnapshotRef,
                        Current.Project, Current.FixedContext, Current.Host, Current.StartedGeneration, Current.CurrentEditorGeneration,
                        Current.DeadlineUtc, Current.StartedAtUtc, Current.UpdatedAtUtc, Current.State, Current.Cursor,
                        Current.Steps, Current.ChildExecutionRefs, Current.Cancellation, Current.TerminalRecordRef);
                    return new ProgramRunStoreCompareExchangeResult(false, Current);
                }
                if (Current.Version != expected.Version)
                {
                    return new ProgramRunStoreCompareExchangeResult(false, Current);
                }
                Current = replacement;
                return new ProgramRunStoreCompareExchangeResult(true, Current);
            }
        }

        public ValueTask<ProgramRunTerminalPublicationResult> PublishRunTerminalAsync (ProgramRunRecord expected, ProgramRunTerminalRecord terminalRecord, Func<ArtifactRef, ProgramRunRecord> createReplacement, CancellationToken cancellationToken = default)
        {
            Operations.Add("publishRunTerminal");
            if (RejectRunTerminalPublications)
            {
                throw new InvalidOperationException("terminal publication conflict");
            }
            LastRunTerminal = terminalRecord;
            var reference = CreateArtifact("programRunTerminalRecord", "run-terminal.json");
            Current = createReplacement(reference);
            return ValueTask.FromResult(new ProgramRunTerminalPublicationResult(true, reference, Current));
        }

        public ValueTask<ProgramRunTerminalPublicationResult> PublishRunTimeoutTerminalAsync (ProgramRunRecord expected, int stepIndex, ProgramRunTerminalRecord terminalRecord, Func<ArtifactRef, ProgramRunRecord> createReplacement, CancellationToken cancellationToken = default)
        {
            Operations.Add("publishRunTimeoutTerminal");
            if (Current.Version != expected.Version || terminalRecord.State != ProgramRunState.Failed
                || terminalRecord.ReasonCode != "PROGRAM_RUN_TIMEOUT"
                || stepIndex < 0 || stepIndex >= Current.Steps.Count
                || Current.Steps[stepIndex].State != ProgramStepState.Planning
                || Current.Steps[stepIndex].ExecutionPortInvoked)
            {
                throw new InvalidOperationException("Run-timeout terminal publication requires the current unadmitted planning Step.");
            }
            LastRunTerminal = terminalRecord;
            var reference = CreateArtifact("programRunTerminalRecord", "run-timeout-terminal.json");
            var replacement = createReplacement(reference);
            var restored = replacement.Steps[stepIndex];
            if (!ProgramRunStateSemantics.IsTerminal(replacement.State)
                || restored.State != ProgramStepState.Deferred
                || restored.PlanningStartedAtUtc != Current.Steps[stepIndex].PlanningStartedAtUtc
                || restored.DeadlineUtc != Current.Steps[stepIndex].DeadlineUtc)
            {
                throw new InvalidOperationException("Run-timeout terminal publication must atomically retain the planning audit facts.");
            }
            Current = replacement;
            return ValueTask.FromResult(new ProgramRunTerminalPublicationResult(true, reference, Current));
        }

        public ValueTask<ProgramRunStepTerminalPublicationResult> PublishStepTerminalAsync (ProgramRunRecord expected, int stepIndex, ProgramStepTerminalRecord terminalRecord, Func<ArtifactRef, ProgramRunRecord> createReplacement, CancellationToken cancellationToken = default)
        {
            Operations.Add("publishStepTerminal");
            StepTerminalPublicationAttempts++;
            if (RejectNextStepTerminalPublication)
            {
                RejectNextStepTerminalPublication = false;
                return ValueTask.FromException<ProgramRunStepTerminalPublicationResult>(new InvalidOperationException("step terminal publication conflict"));
            }
            var reference = CreateArtifact("programStepTerminalRecord", "step-terminal.json");
            Current = createReplacement(reference);
            return ValueTask.FromResult(new ProgramRunStepTerminalPublicationResult(true, reference, Current));
        }

        private static ProgramDefinitionSnapshotFixedDefinition CreateDefinition ()
        {
            using var document = JsonDocument.Parse("{\"steps\":[{\"command\":\"ready\"}]} ");
            return new ProgramDefinitionSnapshotFixedDefinition([new ReadyProgramStep(null)], [], new ProgramSourceManifest(
                Sha256Digest.Parse(new string('e', 64)), ProgramRootSource.Stdin, null, null, Sha256Digest.Parse(new string('f', 64)), []), Sha256Digest.Parse(new string('a', 64)));
        }
    }
}
