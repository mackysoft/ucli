using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MackySoft.Json.Canonicalization;
using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Application.Features.Programs.Resolution;
using MackySoft.Ucli.Application.Features.Programs.Supervision;
using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Programs.Persistence;

namespace MackySoft.Ucli.Tests.Features.Programs.Persistence;

public sealed class FileProgramRunStoreTests
{
    private static readonly DateTimeOffset StartedAtUtc = new(2026, 8, 12, 0, 0, 0, TimeSpan.Zero);
    private static readonly Sha256Digest DefinitionDigest = Sha256Digest.Parse("14c934ffaac9d7cfce1bcda1de4d74cfbc14d35d8f3eae8d119dfb2e84c5c629");

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CreateAndCompareExchange_PreservesOneFixedRunAndRejectsStaleWriter ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "cas");
        var store = new FileProgramRunStore(AbsolutePath.Parse(scope.GetPath("repository")), CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), version: 0, ProgramRunState.Created, cursor: 0, ProgramCancellationRecord.None);

        Assert.True((await store.CreateAsync(initial)).Created);
        Assert.False((await store.CreateAsync(initial)).Created);
        var reloaded = await store.ReadAsync(initial.RunId);
        Assert.NotNull(reloaded);
        Assert.True(reloaded.FixedContext.Authorization.AllowDangerous);
        Assert.Equal("auto", reloaded.FixedContext.ExecutionMode.RequestedMode);
        Assert.Equal(ProgramSupervisorConnection.Connected, reloaded.FixedContext.Supervisor.Connection);

        var running = CreateRun(initial.RunId, version: 1, ProgramRunState.Running, cursor: 0, ProgramCancellationRecord.None, definitionSnapshotRef: initial.DefinitionSnapshotRef);
        Assert.True((await store.CompareExchangeAsync(initial, running)).Exchanged);
        var stale = await store.CompareExchangeAsync(initial, CreateRun(initial.RunId, 1, ProgramRunState.Cancelling, 0, ProgramCancellationRecord.None, definitionSnapshotRef: initial.DefinitionSnapshotRef));

        Assert.False(stale.Exchanged);
        Assert.Equal(ProgramRunState.Running, stale.Current.State);
        Assert.Equal(1, stale.Current.Version);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("reference")]
    [InlineData("invalidBody")]
    [InlineData("runDigest")]
    [Trait("Size", "Medium")]
    public async Task Create_RejectsAnUnverifiedDefinitionSnapshotWithoutWritingState (string corruption)
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", $"create-snapshot-{corruption}");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        var snapshot = Assert.IsType<PathArtifactRef>(initial.DefinitionSnapshotRef);
        var snapshotPath = Path.Combine(repository.Value, snapshot.Path.Value);
        var candidate = initial;

        switch (corruption)
        {
            case "missing":
                File.Delete(snapshotPath);
                break;
            case "reference":
                candidate = CreateRun(initial.RunId, 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None,
                    definitionSnapshotRef: CreateArtifactReference(snapshot, Sha256Digest.Parse(new string('f', 64)), snapshot.SizeBytes));
                break;
            case "invalidBody":
                var invalidBytes = Encoding.UTF8.GetBytes("{\"schemaVersion\":999}");
                await File.WriteAllBytesAsync(snapshotPath, invalidBytes);
                candidate = CreateRun(initial.RunId, 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None,
                    definitionSnapshotRef: CreateArtifactReference(snapshot, Sha256Digest.Compute(invalidBytes), (ulong)invalidBytes.Length));
                break;
            case "runDigest":
                candidate = CreateRun(initial.RunId, 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None,
                    definitionSnapshotRef: snapshot, definitionDigest: Sha256Digest.Parse(new string('f', 64)));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(corruption));
        }

        await Assert.ThrowsAnyAsync<Exception>(() => store.CreateAsync(candidate).AsTask());
        Assert.Empty(Directory.GetFiles(repository.Value, "state.json", SearchOption.AllDirectories));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Create_WhenStateAlreadyExists_UsesExistingReadbackWithoutCandidateSnapshotAdmission ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "create-existing");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        Assert.True((await store.CreateAsync(initial)).Created);
        var snapshot = Assert.IsType<PathArtifactRef>(initial.DefinitionSnapshotRef);
        var malformedCandidate = CreateRun(initial.RunId, 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None,
            definitionSnapshotRef: CreateArtifactReference(snapshot, Sha256Digest.Parse(new string('f', 64)), snapshot.SizeBytes));

        var duplicate = await store.CreateAsync(malformedCandidate);

        Assert.False(duplicate.Created);
        Assert.Equal(initial.RunId, duplicate.Current.RunId);
    }

    [Theory]
    [InlineData("count")]
    [InlineData("command")]
    [InlineData("timeout")]
    [Trait("Size", "Medium")]
    public async Task Create_RejectsStepsThatDoNotBindToTheFixedDefinition (string mismatch)
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", $"create-definition-binding-{mismatch}");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        var step = initial.Steps[0];
        var steps = mismatch switch
        {
            "count" => new[] { step, step },
            "command" => new[] { step with { Command = "refresh" } },
            "timeout" => new[] { step with { TimeoutMilliseconds = 999 } },
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch)),
        };
        var candidate = CreateRun(initial.RunId, 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None,
            definitionSnapshotRef: initial.DefinitionSnapshotRef, stepsOverride: steps);

        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateAsync(candidate).AsTask());
        Assert.Empty(Directory.GetFiles(repository.Value, "state.json", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("count")]
    [InlineData("command")]
    [InlineData("timeout")]
    [Trait("Size", "Medium")]
    public async Task Read_RejectsStateStepsThatDoNotBindToTheFixedDefinition (string mismatch)
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", $"read-definition-binding-{mismatch}");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var statePath = await FindStatePathAsync(repository, initial.RunId);
        var state = JsonNode.Parse(await File.ReadAllTextAsync(statePath))!.AsObject();
        var steps = state["steps"]!.AsArray();
        switch (mismatch)
        {
            case "count":
                steps.Add(steps[0]!.DeepClone());
                break;
            case "command":
                steps[0]!["command"] = "refresh";
                break;
            case "timeout":
                steps[0]!["timeoutMilliseconds"] = 999;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mismatch));
        }
        await File.WriteAllTextAsync(statePath, state.ToJsonString());

        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(initial.RunId).AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CreateAndRead_UseTheCapturedCommandTimeoutForAnImplicitDefinitionStep ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "implicit-timeout");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateImplicitReadyRunAsync(store, Guid.NewGuid(), 1200, new Dictionary<string, int> { ["ready"] = 1200 });

        Assert.True((await store.CreateAsync(initial)).Created);
        Assert.Equal(1200, Assert.Single((await store.ReadAsync(initial.RunId))!.Steps).TimeoutMilliseconds);

        var statePath = await FindStatePathAsync(repository, initial.RunId);
        var state = JsonNode.Parse(await File.ReadAllTextAsync(statePath))!.AsObject();
        state["steps"]![0]!["timeoutMilliseconds"] = 1000;
        await File.WriteAllTextAsync(statePath, state.ToJsonString());

        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(initial.RunId).AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Create_RejectsAnImplicitDefinitionStepWithoutItsCapturedCommandTimeout ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "implicit-timeout-missing");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var candidate = await CreateImplicitReadyRunAsync(store, Guid.NewGuid(), 1000, new Dictionary<string, int>());

        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateAsync(candidate).AsTask());
        Assert.Empty(Directory.GetFiles(repository.Value, "state.json", SearchOption.AllDirectories));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Create_AcceptsAnExplicitDefinitionTimeoutOverADifferentCapturedCommandTimeout ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "explicit-timeout");
        var store = new FileProgramRunStore(AbsolutePath.Parse(scope.GetPath("repository")), CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        var candidate = CreateRun(initial.RunId, 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None,
            fixedContext: CreateFixedContext(commandTimeouts: new Dictionary<string, int> { ["ready"] = 1200 }), definitionSnapshotRef: initial.DefinitionSnapshotRef);

        Assert.True((await store.CreateAsync(candidate)).Created);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CompareExchange_CancellationRequestIsPersistedOnceWithoutExecutingCancellation ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "cancel");
        var project = CreateProject();
        var store = new FileProgramRunStore(AbsolutePath.Parse(scope.GetPath("repository")), project.ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Running, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var requested = CreateRun(initial.RunId, 1, ProgramRunState.Running, 0, initial.Cancellation.Request(StartedAtUtc.AddMinutes(1), "USER_CANCELLED"), definitionSnapshotRef: initial.DefinitionSnapshotRef);

        var result = await store.CompareExchangeAsync(initial, requested);
        var duplicate = await store.CompareExchangeAsync(requested, CreateRun(initial.RunId, 2, ProgramRunState.Running, 0, requested.Cancellation.Request(StartedAtUtc.AddMinutes(2), "ignored"), definitionSnapshotRef: initial.DefinitionSnapshotRef));

        Assert.True(result.Exchanged);
        Assert.True(duplicate.Exchanged);
        Assert.Equal(StartedAtUtc.AddMinutes(1), duplicate.Current.Cancellation.RequestedAtUtc);
        Assert.Equal("USER_CANCELLED", duplicate.Current.Cancellation.ReasonCode);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CompareExchange_CannotReplaceThePersistedProgramStepExecutionIdentity ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "step-execution-identity");
        var store = new FileProgramRunStore(AbsolutePath.Parse(scope.GetPath("repository")), CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var execution = new ProgramStepExecutionReference(Guid.NewGuid(), StartedAtUtc, StartedAtUtc.AddSeconds(10));
        var startedStep = initial.Steps[0] with
        {
            State = ProgramStepState.Planning,
            PlanningStartedAtUtc = StartedAtUtc,
            DeadlineUtc = StartedAtUtc.AddSeconds(10),
            StartedAtUtc = StartedAtUtc,
            Execution = execution,
        };
        var started = CreateRun(initial.RunId, 1, ProgramRunState.Running, 0, ProgramCancellationRecord.None,
            definitionSnapshotRef: initial.DefinitionSnapshotRef, stepOverride: startedStep);

        Assert.True((await store.CompareExchangeAsync(initial, started)).Exchanged);

        var replacedExecution = startedStep with { Execution = execution with { ExecutionId = Guid.NewGuid() } };
        var replacement = CreateRun(initial.RunId, 2, ProgramRunState.Running, 0, ProgramCancellationRecord.None,
            definitionSnapshotRef: initial.DefinitionSnapshotRef, stepOverride: replacedExecution);

        await Assert.ThrowsAsync<ArgumentException>(() => store.CompareExchangeAsync(started, replacement).AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CompareExchange_AllowsOnlyAnUninvokedPlanningStepToReturnToDeferredAndPreservesPlanFacts ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "restore-uninvoked-planning");
        var store = new FileProgramRunStore(AbsolutePath.Parse(scope.GetPath("repository")), CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var execution = new ProgramStepExecutionReference(Guid.NewGuid(), StartedAtUtc, StartedAtUtc.AddSeconds(10));
        var planning = initial.Steps[0] with
        {
            State = ProgramStepState.Planning,
            PlanningStartedAtUtc = StartedAtUtc,
            DeadlineUtc = StartedAtUtc.AddSeconds(10),
            StartedAtUtc = StartedAtUtc,
            Execution = execution,
        };
        var running = CreateRun(initial.RunId, 1, ProgramRunState.Running, 0, ProgramCancellationRecord.None,
            definitionSnapshotRef: initial.DefinitionSnapshotRef, stepOverride: planning);
        Assert.True((await store.CompareExchangeAsync(initial, running)).Exchanged);

        var restoredStep = planning with
        {
            State = ProgramStepState.Deferred,
            PlanningStartedAtUtc = null,
            DeadlineUtc = null,
            StartedAtUtc = null,
            Execution = null,
            ExecutionPortInvoked = false,
        };
        var restored = CreateRun(initial.RunId, 2, ProgramRunState.Running, 0, ProgramCancellationRecord.None,
            definitionSnapshotRef: initial.DefinitionSnapshotRef, stepOverride: restoredStep);
        Assert.True((await store.CompareExchangeAsync(running, restored)).Exchanged);
        var readback = await store.ReadAsync(initial.RunId);
        Assert.Equal(ProgramStepState.Deferred, readback!.Steps[0].State);
        Assert.Null(readback.Steps[0].PlanningStartedAtUtc);
        Assert.Null(readback.Steps[0].DeadlineUtc);
        Assert.Null(readback.Steps[0].Execution);

        Assert.Throws<ArgumentException>(() =>
        {
            _ = CreateRun(initial.RunId, 2, ProgramRunState.Running, 0, ProgramCancellationRecord.None,
                definitionSnapshotRef: initial.DefinitionSnapshotRef,
                stepOverride: planning with
                {
                    State = ProgramStepState.Deferred,
                    StartedAtUtc = null,
                    Execution = null,
                    ExecutionPortInvoked = false,
                }) with
            { TerminalReasonCode = "PROGRAM_RUN_TIMEOUT" };
        });

        var admittedInitial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(admittedInitial);
        var admittedPlanning = planning with { ExecutionPortInvoked = true };
        var admittedRunning = CreateRun(admittedInitial.RunId, 1, ProgramRunState.Running, 0, ProgramCancellationRecord.None,
            definitionSnapshotRef: admittedInitial.DefinitionSnapshotRef, stepOverride: admittedPlanning);
        Assert.True((await store.CompareExchangeAsync(admittedInitial, admittedRunning)).Exchanged);
        var forbiddenRestore = CreateRun(admittedInitial.RunId, 2, ProgramRunState.Running, 0, ProgramCancellationRecord.None,
            definitionSnapshotRef: admittedInitial.DefinitionSnapshotRef, stepOverride: restoredStep);
        await Assert.ThrowsAsync<ArgumentException>(() => store.CompareExchangeAsync(admittedRunning, forbiddenRestore).AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TerminalizeRunTimeout_RestoresOnlyUninvokedPlanningAuditFactsAndReadbackVerifiesTheTerminalAggregate ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "run-timeout-restore");
        var store = new FileProgramRunStore(AbsolutePath.Parse(scope.GetPath("repository")), CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var planning = initial.Steps[0] with
        {
            State = ProgramStepState.Planning,
            PlanningStartedAtUtc = StartedAtUtc,
            DeadlineUtc = StartedAtUtc.AddSeconds(10),
            StartedAtUtc = StartedAtUtc,
            Execution = new ProgramStepExecutionReference(Guid.NewGuid(), StartedAtUtc, StartedAtUtc.AddSeconds(10)),
        };
        var running = CreateRun(initial.RunId, 1, ProgramRunState.Running, 0, ProgramCancellationRecord.None,
            definitionSnapshotRef: initial.DefinitionSnapshotRef, stepOverride: planning);
        Assert.True((await store.CompareExchangeAsync(initial, running)).Exchanged);

        var result = await new ProgramRunTerminalizer(new FakeTimeProvider(StartedAtUtc.AddSeconds(11)))
            .TerminalizeAsync(store, running, ProgramRunState.Failed, "PROGRAM_RUN_TIMEOUT");

        Assert.Equal(ProgramRunState.Failed, result.State);
        Assert.NotNull(result.TerminalRecordRef);
        var readback = await store.ReadAsync(initial.RunId);
        var step = Assert.Single(readback!.Steps);
        Assert.Equal(ProgramStepState.Deferred, step.State);
        Assert.Equal(StartedAtUtc, step.PlanningStartedAtUtc);
        Assert.Equal(StartedAtUtc.AddSeconds(10), step.DeadlineUtc);
        Assert.Null(step.Execution);
        Assert.Null(step.StartedAtUtc);
        Assert.Equal("PROGRAM_RUN_TIMEOUT", readback.TerminalReasonCode);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RequestCancellation_PreservesLivenessObservationsInTheFileStoreReadback ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "cancel-observation-readback");
        var store = new FileProgramRunStore(AbsolutePath.Parse(scope.GetPath("repository")), CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var observed = CreateRun(initial.RunId, 1, ProgramRunState.Created, 0, ProgramCancellationRecord.None,
            definitionSnapshotRef: initial.DefinitionSnapshotRef) with
        {
            SupervisorObservation = new ProgramProcessLivenessObservation(ProcessIdentityStatus.Matching, StartedAtUtc),
            HostObservation = new ProgramProcessLivenessObservation(ProcessIdentityStatus.Unobservable, StartedAtUtc),
        };
        Assert.True((await store.CompareExchangeAsync(initial, observed)).Exchanged);
        var service = new ProgramRunPersistenceService(
            new SingleStoreFactory(store),
            new FixedGuidGenerator(Guid.NewGuid()),
            new FakeTimeProvider(StartedAtUtc.AddSeconds(1)));

        var cancelled = await service.RequestCancellationAsync(CreateResolvedProject(), initial.RunId, "USER_CANCELLED");
        var readback = await store.ReadAsync(initial.RunId);

        Assert.Equal(2, cancelled!.Version);
        Assert.True(readback!.Cancellation.Requested);
        Assert.Equal("USER_CANCELLED", readback.Cancellation.ReasonCode);
        Assert.Equal(ProcessIdentityStatus.Matching, readback.SupervisorObservation!.Status);
        Assert.Equal(ProcessIdentityStatus.Unobservable, readback.HostObservation!.Status);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("step-timeout")]
    [InlineData("run-timeout")]
    [Trait("Size", "Medium")]
    public async Task UnadmittedCallPlanningTerminalization_PreservesEstablishedPlanningReferences (string terminalization)
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", $"call-planning-{terminalization}");
        var store = new FileProgramRunStore(AbsolutePath.Parse(scope.GetPath("repository")), CreateProject().ProjectFingerprint);
        var (initial, planning, plan, descriptors) = await CreateCallPlanningRunAsync(store);
        await store.CreateAsync(initial);
        var running = CreateRun(initial.RunId, 1, ProgramRunState.Running, 0, ProgramCancellationRecord.None,
            definitionSnapshotRef: initial.DefinitionSnapshotRef, stepOverride: planning, hostOverride: initial.Host,
            definitionDigest: initial.DefinitionDigest);
        var runningExchange = await store.CompareExchangeAsync(initial, running);
        Assert.True(runningExchange.Exchanged);
        var current = runningExchange.Current;
        var persistedRunning = await store.ReadAsync(initial.RunId);
        Assert.Equal(current.Steps[0].GenerationBefore, persistedRunning!.Steps[0].GenerationBefore);
        Assert.Equal(current.Steps[0].RequestExecution!.ExecutionId, persistedRunning.Steps[0].RequestExecution!.ExecutionId);
        current = persistedRunning;
        if (terminalization == "cancel")
        {
            var cancelling = new ProgramRunRecord(
                ProgramRunRecord.CurrentSchemaVersion, current.Version + 1, current.RunId, current.DefinitionDigest,
                current.DefinitionSnapshotRef, current.Project, current.FixedContext, current.Host,
                current.StartedGeneration, current.CurrentEditorGeneration, current.DeadlineUtc, current.StartedAtUtc,
                current.UpdatedAtUtc, ProgramRunState.Cancelling, current.Cursor, current.Steps, current.ChildExecutionRefs,
                current.Cancellation.Request(StartedAtUtc, "USER_CANCELLED"), current.TerminalRecordRef);
            Assert.Equal(current.Steps[0].GenerationBefore, cancelling.Steps[0].GenerationBefore);
            Assert.Equal(current.Steps[0].RequestExecution!.ExecutionId, cancelling.Steps[0].RequestExecution!.ExecutionId);
            var cancellationExchange = await store.CompareExchangeAsync(current, cancelling);
            Assert.True(cancellationExchange.Exchanged);
            current = cancellationExchange.Current;
        }
        var terminalizer = new ProgramRunTerminalizer(new FakeTimeProvider(StartedAtUtc.AddSeconds(1)));

        _ = terminalization switch
        {
            "cancel" => await terminalizer.TerminalizeAsync(store, current, ProgramRunState.Cancelled, "PROGRAM_RUN_CANCELLED"),
            "step-timeout" => await terminalizer.TerminalizeUnstartedStepTimeoutAsync(store, current, 0, CancellationToken.None),
            "run-timeout" => await terminalizer.TerminalizeAsync(store, current, ProgramRunState.Failed, "PROGRAM_RUN_TIMEOUT"),
            _ => throw new ArgumentOutOfRangeException(nameof(terminalization)),
        };
        var readback = await store.ReadAsync(initial.RunId);
        var step = Assert.Single(readback!.Steps);

        Assert.Equal(plan, step.RequestPlanRef);
        Assert.Equal(descriptors, step.OperationDescriptorRefs);
        if (terminalization != "cancel")
        {
            Assert.Equal(planning.GenerationBefore, step.GenerationBefore);
            Assert.Equal(planning.RequestExecution!.ExecutionId, step.RequestExecution!.ExecutionId);
            Assert.Equal(planning.RequestExecution.StartedAtUtc, step.RequestExecution.StartedAtUtc);
            Assert.Equal(planning.RequestExecution.DeadlineUtc, step.RequestExecution.DeadlineUtc);
        }
        Assert.Equal(terminalization == "step-timeout" ? ProgramStepState.Failed : ProgramStepState.Deferred, step.State);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Store_RejectsRunsAndStateRetargetedToAnotherProject ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "project-scope");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Running, 0, ProgramCancellationRecord.None);
        var otherProject = new UnityProjectIdentity("/other", new ProjectFingerprint(new string('f', 64)), "6000.1.0f1");
        var foreign = CreateRun(initial.RunId, 0, ProgramRunState.Running, 0, ProgramCancellationRecord.None,
            definitionSnapshotRef: initial.DefinitionSnapshotRef, projectOverride: otherProject);

        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateAsync(foreign).AsTask());
        await store.CreateAsync(initial);
        await Assert.ThrowsAsync<ArgumentException>(() => store.CompareExchangeAsync(foreign,
            CreateRun(initial.RunId, 1, ProgramRunState.Running, 0, ProgramCancellationRecord.None,
                definitionSnapshotRef: initial.DefinitionSnapshotRef, projectOverride: otherProject)).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => store.PublishStepTerminalAsync(foreign, 0, CreateStepTerminal(initial, "FOREIGN"),
            artifact => CreateStepTerminalReplacement(initial, artifact, "FOREIGN")).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => store.PublishRunTerminalAsync(foreign,
            CreateRunTerminal(initial, CreateArtifact("programStepTerminalRecord", "foreign.json"), "FOREIGN"),
            artifact => CreateTerminalRunReplacement(initial, artifact, CreateArtifact("programStepTerminalRecord", "foreign.json"), "FOREIGN")).AsTask());

        var statePath = await FindStatePathAsync(repository, initial.RunId);
        var json = (await File.ReadAllTextAsync(statePath)).Replace(new string('b', 64), new string('f', 64), StringComparison.Ordinal);
        Assert.NotNull(JsonSerializer.Deserialize<ProgramRunRecord>(json, IpcJsonSerializerOptions.Default));
        await File.WriteAllTextAsync(statePath, json);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(initial.RunId).AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishStepTerminal_RejectsStaleStateWithANonterminalResultReference ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "nonterminal-result-reference");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Running, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var statePath = await FindStatePathAsync(repository, initial.RunId);
        var state = JsonNode.Parse(await File.ReadAllTextAsync(statePath))!.AsObject();
        state["steps"]![0]!["resultRef"] = JsonNode.Parse(JsonSerializer.Serialize(CreateArtifact("programStepTerminalRecord", "injected.json"), IpcJsonSerializerOptions.Default));
        await File.WriteAllTextAsync(statePath, state.ToJsonString());

        await Assert.ThrowsAsync<InvalidDataException>(() => store.PublishStepTerminalAsync(initial, 0, CreateStepTerminal(initial, "RETRY"),
            artifact => CreateStepTerminalReplacement(initial, artifact, "RETRY")).AsTask());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgramRunRecord_RejectsUndefinedStepApplicationState ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateRun(
            Guid.NewGuid(),
            0,
            ProgramRunState.Created,
            0,
            ProgramCancellationRecord.None,
            (ExecutionApplicationState)999,
            definitionSnapshotRef: CreateArtifact("programDefinitionSnapshot", "definition.json")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgramRunStepRecord_DeferredRejectsExecutionAndResultFactsButAllowsPlanningFacts ()
    {
        var deferred = new ProgramRunStepRecord("ready", 1000, ProgramStepState.Deferred, null, StartedAtUtc, StartedAtUtc.AddSeconds(10), null, null,
            ExecutionApplicationState.NotApplied, null, [], null, null, null, null, null, [], null, null, null);

        deferred.Validate();
        Assert.Throws<ArgumentException>(() => (deferred with { Verdict = Verdict.Pass }).Validate());
        Assert.Throws<ArgumentException>(() => (deferred with { ApplicationState = ExecutionApplicationState.Applied }).Validate());
        Assert.Throws<ArgumentException>(() => (deferred with { StartedAtUtc = StartedAtUtc }).Validate());
        Assert.Throws<ArgumentException>(() => (deferred with { CompletedAtUtc = StartedAtUtc }).Validate());
        Assert.Throws<ArgumentException>(() => (deferred with { ResultRef = CreateArtifact("programStepTerminalRecord", "result.json") }).Validate());
        Assert.Throws<ArgumentException>(() => (deferred with { StepResultRef = CreateArtifact("stepResult", "result.json") }).Validate());
        Assert.Throws<ArgumentException>(() => (deferred with { ArtifactRefs = [CreateArtifact("stepArtifact", "artifact.json")] }).Validate());
        Assert.Throws<ArgumentException>(() => (deferred with { ErrorCode = "failed" }).Validate());
        Assert.Throws<ArgumentException>(() => (deferred with { LifecycleExecutionRef = new ActiveExecutionRef(new ExecutionKind("request"), Guid.NewGuid(), DefinitionDigest, new ExecutionState("running"), new ExecutionStatusLocator("status")) }).Validate());
        Assert.Throws<ArgumentException>(() => (deferred with { RequestExecution = CreateRequestExecutionBoundary() }).Validate());

        var call = new ProgramRunStepRecord("call", 1000, ProgramStepState.Deferred, null, StartedAtUtc, StartedAtUtc.AddSeconds(10),
            new UnityEditorGenerationSnapshot(1, 2, 3, 4), null, ExecutionApplicationState.NotApplied,
            CreateArtifact("requestPlan", "plan.json"), [CreateArtifact("operationDescriptor", "descriptor.json")], null,
            CreateRequestExecutionBoundary(), null, null, null, [], null, null, null);
        call.Validate();
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("screenshot.game")]
    [InlineData("refresh")]
    [Trait("Size", "Small")]
    public void ProgramRunStepRecord_DeferredRejectsRequestPlanAndDescriptorsOutsideCall (string command)
    {
        var step = new ProgramRunStepRecord(command, 1000, ProgramStepState.Deferred, null, StartedAtUtc, StartedAtUtc.AddSeconds(10), null, null,
            ExecutionApplicationState.NotApplied, CreateArtifact("requestPlan", "plan.json"), [CreateArtifact("operationDescriptor", "descriptor.json")], null, null, null, null, null, [], null, null, null);

        Assert.Throws<ArgumentException>(step.Validate);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgramRunStepRecord_DeferredCallAcceptsRequestPlanAndDescriptors ()
    {
        var step = new ProgramRunStepRecord("call", 1000, ProgramStepState.Deferred, null, StartedAtUtc, StartedAtUtc.AddSeconds(10), null, null,
            ExecutionApplicationState.NotApplied, CreateArtifact("requestPlan", "plan.json"), [CreateArtifact("operationDescriptor", "descriptor.json")], null, null, null, null, null, [], null, null, null);

        step.Validate();
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgramRunStepRecord_RestrictsExecutionReferencesToTheFixedCommand ()
    {
        var request = CreateRequestExecutionBoundary();
        var lifecycle = CreateLifecycleReference(LifecycleExecutionKind.Refresh);
        var call = CreatePlanningStep("call") with { RequestExecution = request };
        call.Validate();
        Assert.Throws<ArgumentException>(() => (call with { LifecycleExecutionRef = lifecycle }).Validate());

        var refresh = CreatePlanningStep("refresh") with { LifecycleExecutionRef = lifecycle };
        refresh.Validate();
        Assert.Throws<ArgumentException>(() => (refresh with { RequestExecution = request }).Validate());
        Assert.Throws<ArgumentException>(() => (refresh with { LifecycleExecutionRef = CreateLifecycleReference(LifecycleExecutionKind.Compile) }).Validate());

        var ready = CreatePlanningStep("ready");
        Assert.Throws<ArgumentException>(() => (ready with { LifecycleExecutionRef = lifecycle }).Validate());
        Assert.Throws<ArgumentException>(() => (ready with { RequestExecution = request }).Validate());
    }

    [Theory]
    [InlineData("refresh", LifecycleExecutionKind.Refresh)]
    [InlineData("compile", LifecycleExecutionKind.Compile)]
    [InlineData("play.enter", LifecycleExecutionKind.PlayEnter)]
    [InlineData("play.exit", LifecycleExecutionKind.PlayExit)]
    [Trait("Size", "Small")]
    public void ProgramRunStepRecord_AcceptsOnlyTheMatchingLifecycleReference (string command, LifecycleExecutionKind kind)
    {
        (CreatePlanningStep(command) with { LifecycleExecutionRef = CreateLifecycleReference(kind) }).Validate();
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("screenshot.game")]
    [InlineData("refresh")]
    [Trait("Size", "Small")]
    public void ProgramRunStepRecord_RejectsRequestPlanAndDescriptorsOutsideCall (string command)
    {
        var step = CreatePlanningStep(command) with
        {
            RequestPlanRef = CreateArtifact("requestPlan", "plan.json"),
            OperationDescriptorRefs = [CreateArtifact("operationDescriptor", "descriptor.json")],
        };

        Assert.Throws<ArgumentException>(step.Validate);
    }

    [Theory]
    [InlineData("refresh", LifecycleExecutionKind.Refresh)]
    [InlineData("compile", LifecycleExecutionKind.Compile)]
    [InlineData("play.enter", LifecycleExecutionKind.PlayEnter)]
    [InlineData("play.exit", LifecycleExecutionKind.PlayExit)]
    [Trait("Size", "Small")]
    public void ProgramRunStepRecord_RejectsSynchronousResultArtifactsForLifecycleSteps (string command, LifecycleExecutionKind kind)
    {
        var result = CreatePlanningStep(command) with
        {
            LifecycleExecutionRef = CreateLifecycleReference(kind),
            StepResultRef = CreateArtifact("stepResult", "result.json"),
        };
        var artifacts = result with { StepResultRef = null, ArtifactRefs = [CreateArtifact("stepArtifact", "artifact.json")] };

        Assert.Throws<ArgumentException>(result.Validate);
        Assert.Throws<ArgumentException>(artifacts.Validate);
    }

    [Theory]
    [InlineData("call")]
    [InlineData("ready")]
    [InlineData("screenshot.game")]
    [InlineData("screenshot.scene")]
    [Trait("Size", "Small")]
    public void ProgramRunStepRecord_AcceptsSynchronousResultArtifactsForCallAndSyncSteps (string command)
    {
        var step = CreatePlanningStep(command) with
        {
            StepResultRef = CreateArtifact("stepResult", "result.json"),
            ArtifactRefs = [CreateArtifact("stepArtifact", "artifact.json")],
        };
        step.Validate();
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgramStepTerminalRecord_RejectsUnknownOrUnorderedTerminalFacts ()
    {
        var valid = CreateStepTerminal(CreateRun(Guid.NewGuid(), 0, ProgramRunState.Running, 0, ProgramCancellationRecord.None,
            definitionSnapshotRef: CreateArtifact("programDefinitionSnapshot", "definition.json")), "FAILED");
        valid.Validate();
        Assert.Throws<ArgumentException>(() => (valid with { Verdict = (Verdict)999 }).Validate());
        Assert.Throws<ArgumentException>(() => (valid with { ApplicationState = (ExecutionApplicationState)999 }).Validate());
        Assert.Throws<ArgumentException>(() => (valid with { StartedAtUtc = new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.FromHours(9)) }).Validate());
        Assert.Throws<ArgumentException>(() => (valid with { StartedAtUtc = valid.CompletedAtUtc.AddSeconds(1) }).Validate());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgramRunRecord_DerivesApplicationStateFromAdmittedStepsByEvidencePrecedence ()
    {
        var deferred = new ProgramRunStepRecord("ready", 1000, ProgramStepState.Deferred, null, null, null, null, null,
            ExecutionApplicationState.NotApplied, null, [], null, null, null, null, null, [], null, null, null);

        Assert.Equal(ExecutionApplicationState.NotApplied, ProgramRunRecord.DeriveApplicationState([deferred]));
        Assert.Equal(ExecutionApplicationState.Applied, ProgramRunRecord.DeriveApplicationState([CreatePlanningStep(ExecutionApplicationState.Applied)]));
        Assert.Equal(ExecutionApplicationState.PartiallyApplied, ProgramRunRecord.DeriveApplicationState([
            CreatePlanningStep(ExecutionApplicationState.Applied), CreatePlanningStep(ExecutionApplicationState.PartiallyApplied),
        ]));
        Assert.Equal(ExecutionApplicationState.Indeterminate, ProgramRunRecord.DeriveApplicationState([
            CreatePlanningStep(ExecutionApplicationState.PartiallyApplied), CreatePlanningStep(ExecutionApplicationState.Indeterminate),
        ]));
        Assert.Equal(ExecutionApplicationState.Unknown, ProgramRunRecord.DeriveApplicationState([
            CreatePlanningStep(ExecutionApplicationState.Applied),
            CreatePlanningStep(ExecutionApplicationState.PartiallyApplied),
            CreatePlanningStep(ExecutionApplicationState.Indeterminate),
            CreatePlanningStep(ExecutionApplicationState.Unknown),
        ]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgramRunRecord_RestrictsTerminalReasonToNonblankTerminalRuns ()
    {
        var snapshot = CreateArtifact("programDefinitionSnapshot", "definition.json");
        var running = CreateRunRecord(0, ProgramRunState.Running, [CreatePlanningStep(ExecutionApplicationState.NotApplied)], snapshot);
        var failed = CreateRunRecord(
            0,
            ProgramRunState.Failed,
            [CreateFailedStep(CreateArtifact("programStepTerminalRecord", "failed.json"))],
            snapshot,
            CreateArtifact("programRunTerminalRecord", "failed-run.json"));

        Assert.Throws<ArgumentException>(() => _ = running with { TerminalReasonCode = "PROGRAM_RUN_TIMEOUT" });
        Assert.Throws<ArgumentException>(() => _ = failed with { TerminalReasonCode = " " });
        Assert.Equal("PROGRAM_RUN_TIMEOUT", (failed with { TerminalReasonCode = "PROGRAM_RUN_TIMEOUT" }).TerminalReasonCode);
        Assert.Null((failed with { TerminalReasonCode = null }).TerminalReasonCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgramRunRecord_RejectsInvalidLivenessObservationsAtInitialization ()
    {
        var run = CreateRunRecord(0, ProgramRunState.Running, [CreatePlanningStep(ExecutionApplicationState.NotApplied)], CreateArtifact("programDefinitionSnapshot", "definition.json"));

        Assert.Throws<ArgumentException>(() => _ = run with
        {
            SupervisorObservation = new ProgramProcessLivenessObservation(ProcessIdentityStatus.Matching, new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.FromHours(9))),
        });
        Assert.Throws<ArgumentException>(() => _ = run with
        {
            HostObservation = new ProgramProcessLivenessObservation((ProcessIdentityStatus)999, StartedAtUtc),
        });
    }

    [Theory]
    [InlineData("supervisorObservation")]
    [InlineData("hostObservation")]
    [Trait("Size", "Medium")]
    public async Task Read_RejectsInvalidLivenessObservationInState (string propertyName)
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "invalid-observation");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var statePath = await FindStatePathAsync(repository, initial.RunId);
        var state = JsonNode.Parse(await File.ReadAllTextAsync(statePath))!.AsObject();
        state[propertyName] = JsonNode.Parse("{\"status\":999,\"observedAtUtc\":\"2026-08-12T00:00:00+00:00\"}");
        await File.WriteAllTextAsync(statePath, state.ToJsonString());

        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(initial.RunId).AsTask());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgramRunRecord_RejectsRequestBoundaryForAnotherProject ()
    {
        var host = new LifecycleExecutionHostRegistration(new ProcessIdentity(101, 1), Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("10000000-0000-0000-0000-000000000002"));
        var generation = new UnityEditorGenerationSnapshot(5, 6, 7, 8);
        var plan = CreateArtifact("requestPlan", "plan.json");
        var boundary = new ProgramRequestExecutionBoundary(Guid.NewGuid(), new UnityProjectIdentity("/other", new ProjectFingerprint(new string('f', 64)), "6000.1.0f1"), host, generation, Sha256Digest.Compute([1]), plan, null, [], [], StartedAtUtc, StartedAtUtc.AddSeconds(10));
        var step = new ProgramRunStepRecord("ready", 1000, ProgramStepState.Running, null, StartedAtUtc, StartedAtUtc.AddSeconds(10), generation, null, ExecutionApplicationState.NotApplied, plan, [], null, boundary, null, null, null, [], null, StartedAtUtc, null);

        Assert.Throws<ArgumentException>(() => CreateRun(Guid.NewGuid(), 0, ProgramRunState.Running, 0, ProgramCancellationRecord.None, stepOverride: step, hostOverride: host, definitionSnapshotRef: CreateArtifact("programDefinitionSnapshot", "definition.json")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Size", "Small")]
    public void ProgramRunRecord_RejectsRequestBoundaryWithDifferentHostOrGeneration (bool hostMismatch)
    {
        var host = CreateHost();
        var generation = new UnityEditorGenerationSnapshot(1, 2, 3, 4);
        var boundary = CreateRequestExecutionBoundary(
            hostMismatch ? CreateHost(processId: 102) : host,
            hostMismatch ? generation : new UnityEditorGenerationSnapshot(5, 6, 7, 8));
        var step = CreatePlanningStep("call") with
        {
            GenerationBefore = generation,
            StartedAtUtc = StartedAtUtc,
            RequestExecution = boundary,
        };

        Assert.Throws<ArgumentException>(() => CreateRun(Guid.NewGuid(), 0, ProgramRunState.Running, 0, ProgramCancellationRecord.None,
            stepOverride: step, hostOverride: host, definitionSnapshotRef: CreateArtifact("programDefinitionSnapshot", "definition.json")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgramRunRecord_DerivesVerdictFromStepFactsWithoutStateSpecificFabrication ()
    {
        var completed = CreateFailedStep(CreateArtifact("programStepTerminalRecord", "completed.json")) with { State = ProgramStepState.Completed, Verdict = Verdict.Incomplete };
        var completedRun = CreateRunRecord(1, ProgramRunState.Completed, [completed], CreateArtifact("programDefinitionSnapshot", "definition.json"), CreateArtifact("programRunTerminalRecord", "run.json"));
        Assert.Equal(Verdict.Incomplete, completedRun.Verdict);

        var failed = CreateFailedStep(CreateArtifact("programStepTerminalRecord", "failed.json")) with { Verdict = Verdict.Pass };
        var failedRun = CreateRunRecord(0, ProgramRunState.Failed, [failed], CreateArtifact("programDefinitionSnapshot", "definition.json"), CreateArtifact("programRunTerminalRecord", "run.json"));
        Assert.Equal(Verdict.Pass, failedRun.Verdict);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgramRunRecord_RejectsCursorThatSkipsDeferredOrOngoingSteps ()
    {
        var snapshot = CreateArtifact("programDefinitionSnapshot", "definition.json");
        var planning = new ProgramRunStepRecord("ready", 1000, ProgramStepState.Planning, null, StartedAtUtc, StartedAtUtc.AddSeconds(10), null, null,
            ExecutionApplicationState.NotApplied, null, [], null, null, null, null, null, [], null, null, null);
        var deferred = new ProgramRunStepRecord("ready", 1000, ProgramStepState.Deferred, null, null, null, null, null,
            ExecutionApplicationState.NotApplied, null, [], null, null, null, null, null, [], null, null, null);

        Assert.Throws<ArgumentException>(() => CreateRunRecord(1, ProgramRunState.Running, [planning, deferred], snapshot));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgramRunRecord_AcceptsCompletedPrefixAndDeferredSuffixAfterFailedCurrentStep ()
    {
        var snapshot = CreateArtifact("programDefinitionSnapshot", "definition.json");
        var completed = new ProgramRunStepRecord("ready", 1000, ProgramStepState.Completed, null, StartedAtUtc, StartedAtUtc.AddSeconds(10), null, null,
            ExecutionApplicationState.NotApplied, null, [], null, null, null, CreateArtifact("programStepTerminalRecord", "completed.json"), null, [], null, null, StartedAtUtc.AddSeconds(10));
        var failed = new ProgramRunStepRecord("ready", 1000, ProgramStepState.Failed, null, StartedAtUtc, StartedAtUtc.AddSeconds(10), null, null,
            ExecutionApplicationState.NotApplied, null, [], null, null, null, CreateArtifact("programStepTerminalRecord", "failed.json"), null, [], null, null, StartedAtUtc.AddSeconds(10));
        var deferred = new ProgramRunStepRecord("ready", 1000, ProgramStepState.Deferred, null, null, null, null, null,
            ExecutionApplicationState.NotApplied, null, [], null, null, null, null, null, [], null, null, null);

        var run = CreateRunRecord(1, ProgramRunState.Failed, [completed, failed, deferred], snapshot, CreateArtifact("programRunTerminalRecord", "run.json"));

        Assert.Equal(1, run.Cursor);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgramRunRecord_AcceptsCompletedPrefixBeforeTheCurrentOngoingStep ()
    {
        var snapshot = CreateArtifact("programDefinitionSnapshot", "definition.json");
        var completed = new ProgramRunStepRecord("ready", 1000, ProgramStepState.Completed, null, StartedAtUtc, StartedAtUtc.AddSeconds(10), null, null,
            ExecutionApplicationState.NotApplied, null, [], null, null, null, CreateArtifact("programStepTerminalRecord", "completed.json"), null, [], null, null, StartedAtUtc.AddSeconds(10));
        var planning = new ProgramRunStepRecord("ready", 1000, ProgramStepState.Planning, null, StartedAtUtc, StartedAtUtc.AddSeconds(10), null, null,
            ExecutionApplicationState.NotApplied, null, [], null, null, null, null, null, [], null, null, null);
        var deferred = new ProgramRunStepRecord("ready", 1000, ProgramStepState.Deferred, null, null, null, null, null,
            ExecutionApplicationState.NotApplied, null, [], null, null, null, null, null, [], null, null, null);

        var run = CreateRunRecord(1, ProgramRunState.Running, [completed, planning, deferred], snapshot);

        Assert.Equal(1, run.Cursor);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgramRunRecord_CompletedRequiresEveryStepToBeCompletedAndConsumed ()
    {
        var snapshot = CreateArtifact("programDefinitionSnapshot", "definition.json");
        var completed = new ProgramRunStepRecord("ready", 1000, ProgramStepState.Completed, null, StartedAtUtc, StartedAtUtc.AddSeconds(10), null, null,
            ExecutionApplicationState.NotApplied, null, [], null, null, null, CreateArtifact("programStepTerminalRecord", "completed.json"), null, [], null, null, StartedAtUtc.AddSeconds(10));
        var terminal = CreateArtifact("programRunTerminalRecord", "run.json");

        Assert.Throws<ArgumentException>(() => CreateRunRecord(0, ProgramRunState.Completed, [completed], snapshot, terminal));
        Assert.Equal(1, CreateRunRecord(1, ProgramRunState.Completed, [completed], snapshot, terminal).Cursor);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CompareExchange_RejectsChangingFixedAuthorizationContext ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "fixed-context");
        var project = CreateProject();
        var store = new FileProgramRunStore(AbsolutePath.Parse(scope.GetPath("repository")), project.ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var changed = CreateRun(initial.RunId, 1, ProgramRunState.Running, 0, ProgramCancellationRecord.None, fixedContext: CreateFixedContext(allowDangerous: false), definitionSnapshotRef: initial.DefinitionSnapshotRef);

        await Assert.ThrowsAsync<ArgumentException>(() => store.CompareExchangeAsync(initial, changed).AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CompareExchange_RejectsReplacingAdmittedStepFactsAndCancellationFacts ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "append-only");
        var store = new FileProgramRunStore(AbsolutePath.Parse(scope.GetPath("repository")), CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Running, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);

        var changedPlanning = CreateRun(initial.RunId, 1, ProgramRunState.Running, 0, ProgramCancellationRecord.None,
            planningStartedAtUtc: StartedAtUtc.AddSeconds(1), definitionSnapshotRef: initial.DefinitionSnapshotRef);
        await Assert.ThrowsAsync<ArgumentException>(() => store.CompareExchangeAsync(initial, changedPlanning).AsTask());

        var requested = CreateRun(initial.RunId, 1, ProgramRunState.Running, 0,
            new ProgramCancellationRecord(true, StartedAtUtc.AddMinutes(1), "first"), definitionSnapshotRef: initial.DefinitionSnapshotRef);
        Assert.True((await store.CompareExchangeAsync(initial, requested)).Exchanged);
        var changedCancellation = CreateRun(initial.RunId, 2, ProgramRunState.Running, 0,
            new ProgramCancellationRecord(true, StartedAtUtc.AddMinutes(2), "second"), definitionSnapshotRef: initial.DefinitionSnapshotRef);
        await Assert.ThrowsAsync<ArgumentException>(() => store.CompareExchangeAsync(requested, changedCancellation).AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishStepTerminal_AllowsAnActiveLifecycleReferenceToMatureToItsSameTerminalReference ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "lifecycle-terminal-maturation");
        var store = new FileProgramRunStore(AbsolutePath.Parse(scope.GetPath("repository")), CreateProject().ProjectFingerprint);
        var active = await CreateActiveRefreshRunAsync(store);
        var terminal = CreateRefreshStepTerminal(active.Run, active.TerminalReference);

        var publication = await store.PublishStepTerminalAsync(active.Run, 0, terminal, artifact =>
            CreateRun(
                active.Run.RunId,
                active.Run.Version + 1,
                ProgramRunState.Running,
                1,
                ProgramCancellationRecord.None,
                fixedContext: active.Run.FixedContext,
                hostOverride: active.Run.Host,
                definitionSnapshotRef: active.Run.DefinitionSnapshotRef,
                definitionDigest: active.Run.DefinitionDigest,
                stepOverride: active.Run.Steps[0] with
                {
                    State = ProgramStepState.Completed,
                    ApplicationState = ExecutionApplicationState.Applied,
                    GenerationAfter = active.Run.StartedGeneration,
                    LifecycleExecutionRef = active.TerminalReference,
                    ResultRef = artifact,
                    CompletedAtUtc = StartedAtUtc.AddSeconds(1),
                }));

        var step = Assert.Single(publication.Current.Steps);
        Assert.Equal(ProgramStepState.Completed, step.State);
        Assert.Equal(active.TerminalReference, step.LifecycleExecutionRef);
        Assert.Equal(publication.TerminalRecordRef, step.ResultRef);

        var terminalToActive = CreateRun(
            publication.Current.RunId,
            publication.Current.Version + 1,
            ProgramRunState.Running,
            1,
            ProgramCancellationRecord.None,
            fixedContext: publication.Current.FixedContext,
            hostOverride: publication.Current.Host,
            definitionSnapshotRef: publication.Current.DefinitionSnapshotRef,
            definitionDigest: publication.Current.DefinitionDigest,
            stepOverride: step with { LifecycleExecutionRef = active.ActiveReference });
        await Assert.ThrowsAsync<ArgumentException>(() => store.CompareExchangeAsync(publication.Current, terminalToActive).AsTask());
    }

    [Theory]
    [InlineData("removed")]
    [InlineData("differentTerminal")]
    [InlineData("recovery")]
    [Trait("Size", "Medium")]
    public async Task CompareExchange_RejectsReplacingAnEstablishedActiveLifecycleReferenceExceptItsSameTerminalMaturation (string replacementKind)
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", $"lifecycle-ref-{replacementKind}");
        var store = new FileProgramRunStore(AbsolutePath.Parse(scope.GetPath("repository")), CreateProject().ProjectFingerprint);
        var active = await CreateActiveRefreshRunAsync(store);
        ExecutionRef? replacementReference = replacementKind switch
        {
            "removed" => null,
            "differentTerminal" => CreateRefreshTerminalReference(Guid.NewGuid()),
            "recovery" => new RecoveryExecutionRef(
                active.ActiveReference.Kind,
                active.ActiveReference.Id,
                active.ActiveReference.DefinitionDigest,
                new ExecutionState("recovering"),
                new ExecutionStatusLocator("lifecycle-execution/refresh/recovery.json")),
            _ => throw new ArgumentOutOfRangeException(nameof(replacementKind)),
        };
        var replacement = CreateRun(
            active.Run.RunId,
            active.Run.Version + 1,
            ProgramRunState.Running,
            0,
            ProgramCancellationRecord.None,
            fixedContext: active.Run.FixedContext,
            hostOverride: active.Run.Host,
            definitionSnapshotRef: active.Run.DefinitionSnapshotRef,
            definitionDigest: active.Run.DefinitionDigest,
            stepOverride: active.Run.Steps[0] with { LifecycleExecutionRef = replacementReference });

        await Assert.ThrowsAsync<ArgumentException>(() => store.CompareExchangeAsync(active.Run, replacement).AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishRunTerminal_PublishesVerifiedRecordBeforeTerminalExecutionReference ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "terminal-publication");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var project = CreateProject();
        var store = new FileProgramRunStore(repository, project.ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Running, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var stepPublication = await store.PublishStepTerminalAsync(initial, 0, CreateStepTerminal(initial, "FAILED"),
            artifact => CreateStepTerminalReplacement(initial, artifact, "FAILED"));
        var admitted = stepPublication.Current;
        var stepTerminalRef = stepPublication.TerminalRecordRef;
        var terminal = CreateRunTerminal(admitted, stepTerminalRef, "FAILED");

        var publication = await store.PublishRunTerminalAsync(
            admitted,
            terminal,
            artifact => CreateTerminalRunReplacement(admitted, artifact, stepTerminalRef, "FAILED"));

        var duplicate = await store.PublishRunTerminalAsync(
            admitted,
            terminal,
            artifact => CreateTerminalRunReplacement(admitted, artifact, stepTerminalRef, "FAILED"));

        var readback = await store.ReadAsync(initial.RunId);
        Assert.NotNull(readback);
        Assert.Equal(publication.TerminalRecordRef, readback.TerminalRecordRef);
        Assert.Equal(publication.TerminalRecordRef.Digest, duplicate.TerminalRecordRef.Digest);
        await MackySoft.Ucli.Infrastructure.Artifacts.ImmutableArtifactFileVerifier.VerifyAsync(repository, publication.TerminalRecordRef, default);
        var execution = Assert.IsType<TerminalExecutionRef>(readback.CreateExecutionReference(new ExecutionStatusLocator("status")));
        Assert.Equal(publication.TerminalRecordRef, execution.TerminalRecordRef);

        var path = Assert.IsType<PathArtifactRef>(publication.TerminalRecordRef).Path;
        await File.WriteAllTextAsync(Path.Combine(repository.Value, path.Value), "{\"schemaVersion\":999}");
        await Assert.ThrowsAsync<IOException>(() => MackySoft.Ucli.Infrastructure.Artifacts.ImmutableArtifactFileVerifier.VerifyAsync(repository, publication.TerminalRecordRef, default).AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Reconciliation_WhenAStaleTerminalPublisherRaces_ReturnsTheExistingFileStoreTerminal ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "reconcile-terminal-stale");
        var project = CreateProject();
        var store = new FileProgramRunStore(AbsolutePath.Parse(scope.GetPath("repository")), project.ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Running, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var step = await store.PublishStepTerminalAsync(initial, 0, CreateStepTerminal(initial, "FIRST"),
            artifact => CreateStepTerminalReplacement(initial, artifact, "FIRST"));
        var terminal = CreateRunTerminal(step.Current, step.TerminalRecordRef, "FIRST");
        var published = await store.PublishRunTerminalAsync(step.Current, terminal,
            artifact => CreateTerminalRunReplacement(step.Current, artifact, step.TerminalRecordRef, "FIRST"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.PublishRunTerminalAsync(step.Current,
            CreateRunTerminal(step.Current, step.TerminalRecordRef, "SECOND"),
            artifact => CreateTerminalRunReplacement(step.Current, artifact, step.TerminalRecordRef, "SECOND")).AsTask());

        var reconciliation = new ProgramRunStatusCancelReconciliationService(
            new SingleStoreFactory(store), new MatchingObserver(), new FakeTimeProvider(StartedAtUtc));
        var result = await reconciliation.GetStatusAsync(CreateResolvedProject(), initial.RunId);

        Assert.NotNull(result);
        Assert.Equal(ProgramRunState.Failed, result!.State);
        Assert.Equal(published.TerminalRecordRef, result.TerminalRecordRef);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Size", "Medium")]
    public async Task PublishRunTerminal_RejectsDeadlineOrSourceManifestThatDiffersFromTheVerifiedDefinition (bool changeDeadline)
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "terminal-fixed-definition");
        var store = new FileProgramRunStore(AbsolutePath.Parse(scope.GetPath("repository")), CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Running, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var terminal = CreateRunTerminal(initial, CreateArtifact("programStepTerminalRecord", "step-terminal.json"), "FAILED") with
        {
            DeadlineUtc = changeDeadline ? initial.DeadlineUtc.AddSeconds(1) : initial.DeadlineUtc,
            SourceManifest = changeDeadline ? CreateSnapshotManifest() : CreateSnapshotManifest() with { Digest = Sha256Digest.Parse(new string('f', 64)) },
        };

        await Assert.ThrowsAsync<ArgumentException>(() => store.PublishRunTerminalAsync(initial, terminal,
            artifact => CreateTerminalRunReplacement(initial, artifact, CreateArtifact("programStepTerminalRecord", "step-terminal.json"), "FAILED")).AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishRunTerminal_WithDifferentContentForTheSameExpectedVersion_RejectsTheLaterWriterAndPreservesTheFirst ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "run-terminal-conflict");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Running, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var stepPublication = await store.PublishStepTerminalAsync(initial, 0, CreateStepTerminal(initial, "FIRST"),
            artifact => CreateStepTerminalReplacement(initial, artifact, "FIRST"));
        var admitted = stepPublication.Current;
        var stepTerminalRef = stepPublication.TerminalRecordRef;
        var first = CreateRunTerminal(admitted, stepTerminalRef, "FIRST");
        var firstPublication = await store.PublishRunTerminalAsync(admitted, first,
            artifact => CreateTerminalRunReplacement(admitted, artifact, stepTerminalRef, "FIRST"));
        var second = CreateRunTerminal(admitted, stepTerminalRef, "SECOND");

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.PublishRunTerminalAsync(admitted, second,
            artifact => CreateTerminalRunReplacement(admitted, artifact, stepTerminalRef, "SECOND")).AsTask());

        var readback = await store.ReadAsync(initial.RunId);
        Assert.Equal(firstPublication.TerminalRecordRef, readback!.TerminalRecordRef);
        await MackySoft.Ucli.Infrastructure.Artifacts.ImmutableArtifactFileVerifier.VerifyAsync(repository, firstPublication.TerminalRecordRef, default);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishRunTerminal_RejectsStaleAttemptBeforeCreatingItsArtifactAndAdmitsCurrentContent ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "run-terminal-stale");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var current = CreateRun(initial.RunId, 1, ProgramRunState.Running, 0, ProgramCancellationRecord.None, definitionSnapshotRef: initial.DefinitionSnapshotRef);
        Assert.True((await store.CompareExchangeAsync(initial, current)).Exchanged);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.PublishRunTerminalAsync(initial,
            CreateRunTerminal(initial, CreateArtifact("programStepTerminalRecord", "stale.json"), "STALE"),
            artifact => CreateTerminalRunReplacement(initial, artifact, CreateArtifact("programStepTerminalRecord", "stale.json"), "STALE")).AsTask());
        Assert.Empty(Directory.GetDirectories(repository.Value, "terminal", SearchOption.AllDirectories));

        var publication = await store.PublishRunTerminalAsync(current,
            CreateRunTerminal(current, CreateArtifact("programStepTerminalRecord", "current.json"), "CURRENT"),
            artifact => CreateTerminalRunReplacement(current, artifact, CreateArtifact("programStepTerminalRecord", "current.json"), "CURRENT"));

        Assert.Equal(ProgramRunState.Failed, publication.Current.State);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishRunTerminal_WhenCallbackOrReplacementTemplateFails_DoesNotPublishAndAllowsDifferentContent ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "run-terminal-verify-failure");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Running, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var callbackTerminal = CreateRunTerminal(initial, CreateArtifact("programStepTerminalRecord", "callback.json"), "CALLBACK");
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.PublishRunTerminalAsync(initial, callbackTerminal,
            _ => throw new InvalidOperationException("callback failed")).AsTask());

        var mismatchTerminal = CreateRunTerminal(initial, CreateArtifact("programStepTerminalRecord", "mismatch.json"), "MISMATCH");
        await Assert.ThrowsAsync<ArgumentException>(() => store.PublishRunTerminalAsync(initial, mismatchTerminal,
            artifact => CreateTerminalRunReplacement(initial, artifact, CreateArtifact("programStepTerminalRecord", "mismatch.json"), "OTHER")).AsTask());

        var snapshotPath = Path.Combine(repository.Value, Assert.IsType<PathArtifactRef>(initial.DefinitionSnapshotRef).Path.Value);
        Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(snapshotPath)!, "terminal")));

        var publication = await store.PublishRunTerminalAsync(initial,
            CreateRunTerminal(initial, CreateArtifact("programStepTerminalRecord", "recovered.json"), "RECOVERED"),
            artifact => CreateTerminalRunReplacement(initial, artifact, CreateArtifact("programStepTerminalRecord", "recovered.json"), "RECOVERED"));

        Assert.Equal(ProgramRunState.Failed, publication.Current.State);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishRunTerminal_WhenStateWriteFails_LeavesAnUnreferencedCandidateAndAllowsDifferentContent ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "run-terminal-write-failure");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Running, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var statePath = Assert.Single(Directory.GetFiles(repository.Value, "state.json", SearchOption.AllDirectories));
        var originalState = await File.ReadAllTextAsync(statePath);
        PathArtifactRef? orphanTerminalRef = null;

        await Assert.ThrowsAsync<IOException>(() => store.PublishRunTerminalAsync(initial,
            CreateRunTerminal(initial, CreateArtifact("programStepTerminalRecord", "orphan.json"), "ORPHAN"), artifact =>
            {
                orphanTerminalRef = Assert.IsType<PathArtifactRef>(artifact);
                File.Delete(statePath);
                Directory.CreateDirectory(statePath);
                return CreateTerminalRunReplacement(initial, artifact, CreateArtifact("programStepTerminalRecord", "orphan.json"), "ORPHAN");
            }).AsTask());

        Directory.Delete(statePath);
        await File.WriteAllTextAsync(statePath, originalState);
        var restored = await store.ReadAsync(initial.RunId);
        Assert.Null(restored!.TerminalRecordRef);

        var publication = await store.PublishRunTerminalAsync(restored,
            CreateRunTerminal(restored, CreateArtifact("programStepTerminalRecord", "recovered.json"), "RECOVERED"),
            artifact => CreateTerminalRunReplacement(restored, artifact, CreateArtifact("programStepTerminalRecord", "recovered.json"), "RECOVERED"));

        var recoveredTerminalPath = ContainedPath.Create(repository, RootRelativePath.Parse(Assert.IsType<PathArtifactRef>(publication.TerminalRecordRef).Path.Value)).Target;
        var orphanTerminalPath = ContainedPath.Create(repository, RootRelativePath.Parse(Assert.IsType<PathArtifactRef>(orphanTerminalRef).Path.Value)).Target;
        var terminalFiles = Directory.GetFiles(repository.Value, "*.json", SearchOption.AllDirectories)
            .Where(static path => Path.GetDirectoryName(path)!.EndsWith($"{Path.DirectorySeparatorChar}terminal", StringComparison.Ordinal))
            .Select(AbsolutePath.Parse)
            .ToArray();
        Assert.Equal(2, terminalFiles.Length);
        Assert.Contains(terminalFiles, path => path.IsSameAs(recoveredTerminalPath));
        Assert.False(orphanTerminalPath.IsSameAs(recoveredTerminalPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishStepTerminal_WithDifferentContentForTheSameExpectedVersion_RejectsTheLaterWriterAndPreservesTheFirst ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "step-terminal-conflict");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Running, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var first = CreateStepTerminal(initial, "FIRST");
        var firstPublication = await store.PublishStepTerminalAsync(initial, 0, first,
            artifact => CreateStepTerminalReplacement(initial, artifact, "FIRST"));
        var duplicate = await store.PublishStepTerminalAsync(initial, 0, first,
            artifact => CreateStepTerminalReplacement(initial, artifact, "FIRST"));
        var second = CreateStepTerminal(initial, "SECOND");

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.PublishStepTerminalAsync(initial, 0, second,
            artifact => CreateStepTerminalReplacement(initial, artifact, "SECOND")).AsTask());

        var readback = await store.ReadAsync(initial.RunId);
        Assert.Equal(firstPublication.TerminalRecordRef, readback!.Steps[0].ResultRef);
        Assert.Equal(firstPublication.TerminalRecordRef.Digest, duplicate.TerminalRecordRef.Digest);
        Assert.Equal(firstPublication.TerminalRecordRef.SizeBytes, duplicate.TerminalRecordRef.SizeBytes);
        Assert.Equal(Assert.IsType<PathArtifactRef>(firstPublication.TerminalRecordRef).Path, Assert.IsType<PathArtifactRef>(duplicate.TerminalRecordRef).Path);
        await MackySoft.Ucli.Infrastructure.Artifacts.ImmutableArtifactFileVerifier.VerifyAsync(repository, firstPublication.TerminalRecordRef, default);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishStepTerminal_RejectsStaleAttemptBeforeCreatingItsArtifactAndAdmitsCurrentContent ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "step-terminal-stale");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var current = CreateRun(initial.RunId, 1, ProgramRunState.Running, 0, ProgramCancellationRecord.None, definitionSnapshotRef: initial.DefinitionSnapshotRef);
        Assert.True((await store.CompareExchangeAsync(initial, current)).Exchanged);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.PublishStepTerminalAsync(initial, 0, CreateStepTerminal(initial, "STALE"),
            artifact => CreateStepTerminalReplacement(initial, artifact, "STALE")).AsTask());
        Assert.Empty(Directory.GetDirectories(repository.Value, "terminal", SearchOption.AllDirectories));

        var publication = await store.PublishStepTerminalAsync(current, 0, CreateStepTerminal(current, "CURRENT"),
            artifact => CreateStepTerminalReplacement(current, artifact, "CURRENT"));

        Assert.Equal(ProgramStepState.Failed, publication.Current.Steps[0].State);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishStepTerminal_WhenCallbackOrReplacementTemplateFails_DoesNotPublishAndAllowsDifferentContent ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "step-terminal-verify-failure");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Running, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.PublishStepTerminalAsync(initial, 0, CreateStepTerminal(initial, "CALLBACK"),
            _ => throw new InvalidOperationException("callback failed")).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => store.PublishStepTerminalAsync(initial, 0, CreateStepTerminal(initial, "MISMATCH"),
            artifact => CreateStepTerminalReplacement(initial, artifact, "OTHER")).AsTask());

        var snapshotPath = Path.Combine(repository.Value, Assert.IsType<PathArtifactRef>(initial.DefinitionSnapshotRef).Path.Value);
        Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(snapshotPath)!, "steps", "0", "terminal")));

        var publication = await store.PublishStepTerminalAsync(initial, 0, CreateStepTerminal(initial, "RECOVERED"),
            artifact => CreateStepTerminalReplacement(initial, artifact, "RECOVERED"));

        Assert.Equal(ProgramStepState.Failed, publication.Current.Steps[0].State);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishStepTerminal_WhenStateWriteFails_LeavesAnUnreferencedCandidateAndAllowsDifferentContent ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "step-terminal-write-failure");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Running, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var statePath = Assert.Single(Directory.GetFiles(repository.Value, "state.json", SearchOption.AllDirectories));
        var originalState = await File.ReadAllTextAsync(statePath);
        PathArtifactRef? orphanTerminalRef = null;

        await Assert.ThrowsAsync<IOException>(() => store.PublishStepTerminalAsync(initial, 0, CreateStepTerminal(initial, "ORPHAN"), artifact =>
            {
                orphanTerminalRef = Assert.IsType<PathArtifactRef>(artifact);
                File.Delete(statePath);
                Directory.CreateDirectory(statePath);
                return CreateStepTerminalReplacement(initial, artifact, "ORPHAN");
            }).AsTask());

        Directory.Delete(statePath);
        await File.WriteAllTextAsync(statePath, originalState);
        var restored = await store.ReadAsync(initial.RunId);
        Assert.False(ProgramRunStateSemantics.IsTerminal(restored!.Steps[0].State));

        var publication = await store.PublishStepTerminalAsync(restored, 0, CreateStepTerminal(restored, "RECOVERED"),
            artifact => CreateStepTerminalReplacement(restored, artifact, "RECOVERED"));

        var recoveredTerminalPath = ContainedPath.Create(repository, RootRelativePath.Parse(Assert.IsType<PathArtifactRef>(publication.TerminalRecordRef).Path.Value)).Target;
        var orphanTerminalPath = ContainedPath.Create(repository, RootRelativePath.Parse(Assert.IsType<PathArtifactRef>(orphanTerminalRef).Path.Value)).Target;
        var terminalFiles = Directory.GetFiles(repository.Value, "*.json", SearchOption.AllDirectories)
            .Where(static path => Path.GetDirectoryName(path)!.EndsWith($"{Path.DirectorySeparatorChar}terminal", StringComparison.Ordinal))
            .Select(AbsolutePath.Parse)
            .ToArray();
        Assert.Equal(2, terminalFiles.Length);
        Assert.Contains(terminalFiles, path => path.IsSameAs(recoveredTerminalPath));
        Assert.False(orphanTerminalPath.IsSameAs(recoveredTerminalPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgramRunTerminalRecord_RejectsApplicationStateThatDoesNotMatchItsSteps ()
    {
        var initial = CreateRun(Guid.NewGuid(), 0, ProgramRunState.Running, 0, ProgramCancellationRecord.None,
            definitionSnapshotRef: CreateArtifact("programDefinitionSnapshot", "definition.json"));
        var terminal = CreateRunTerminal(initial, CreateArtifact("programStepTerminalRecord", "step-terminal.json"), "FAILED") with
        {
            ApplicationState = ExecutionApplicationState.Applied,
        };

        Assert.Throws<ArgumentException>(terminal.Validate);
        var valid = CreateRunTerminal(initial, CreateArtifact("programStepTerminalRecord", "step-terminal.json"), "FAILED");
        Assert.Throws<ArgumentException>(() => (valid with { Verdict = (Verdict)999 }).Validate());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_RejectsRunTerminalRecordRetargetedToAnotherValidRun ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "run-terminal-retarget");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var first = await PublishTerminalRunAsync(store, Guid.NewGuid(), "FIRST");
        var second = await PublishTerminalRunAsync(store, Guid.NewGuid(), "SECOND");
        var statePath = await FindStatePathAsync(repository, first.Run.RunId);
        var state = JsonNode.Parse(await File.ReadAllTextAsync(statePath))!.AsObject();
        state["terminalRecordRef"] = JsonNode.Parse(JsonSerializer.Serialize(second.RunTerminalRef, IpcJsonSerializerOptions.Default));
        var retargeted = state.ToJsonString();
        Assert.NotNull(JsonSerializer.Deserialize<ProgramRunRecord>(retargeted, IpcJsonSerializerOptions.Default));
        await File.WriteAllTextAsync(statePath, retargeted);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(first.Run.RunId).AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_RejectsStepTerminalRecordRetargetedToAnotherValidRun ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "step-terminal-retarget");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var first = await PublishTerminalStepAsync(store, Guid.NewGuid(), "FIRST");
        var second = await PublishTerminalStepAsync(store, Guid.NewGuid(), "SECOND");
        var statePath = await FindStatePathAsync(repository, first.Run.RunId);
        var state = JsonNode.Parse(await File.ReadAllTextAsync(statePath))!.AsObject();
        state["steps"]![0]!["resultRef"] = JsonNode.Parse(JsonSerializer.Serialize(second.StepTerminalRef, IpcJsonSerializerOptions.Default));
        var retargeted = state.ToJsonString();
        Assert.NotNull(JsonSerializer.Deserialize<ProgramRunRecord>(retargeted, IpcJsonSerializerOptions.Default));
        await File.WriteAllTextAsync(statePath, retargeted);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(first.Run.RunId).AsTask());
    }

    [Theory]
    [InlineData("digest")]
    [InlineData("size")]
    [InlineData("body")]
    [Trait("Size", "Medium")]
    public async Task Read_RejectsTamperedRunTerminalArtifactIdentityOrBody (string tamper)
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", $"run-terminal-{tamper}");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var published = await PublishTerminalRunAsync(store, Guid.NewGuid(), "VALID");
        var statePath = await FindStatePathAsync(repository, published.Run.RunId);
        var state = JsonNode.Parse(await File.ReadAllTextAsync(statePath))!.AsObject();
        var reference = state["terminalRecordRef"]!.AsObject();

        switch (tamper)
        {
            case "digest":
                reference["digest"] = new string('f', 64);
                break;
            case "size":
                reference["sizeBytes"] = reference["sizeBytes"]!.GetValue<long>() + 1;
                break;
            case "body":
                var path = Assert.IsType<PathArtifactRef>(published.RunTerminalRef).Path;
                var terminalPath = Path.Combine(repository.Value, path.Value);
                var terminal = JsonNode.Parse(await File.ReadAllTextAsync(terminalPath))!.AsObject();
                terminal["steps"]![0]!["errorCode"] = "TAMPERED";
                var bytes = Encoding.UTF8.GetBytes(terminal.ToJsonString());
                await File.WriteAllBytesAsync(terminalPath, bytes);
                reference["digest"] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                reference["sizeBytes"] = bytes.Length;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(tamper));
        }
        var tampered = state.ToJsonString();
        Assert.NotNull(JsonSerializer.Deserialize<ProgramRunRecord>(tampered, IpcJsonSerializerOptions.Default));
        await File.WriteAllTextAsync(statePath, tampered);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(published.Run.RunId).AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_RejectsCorruptStateAndDefinitionSnapshotBytes ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "read-corruption");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var statePath = Assert.Single(Directory.GetFiles(repository.Value, "state.json", SearchOption.AllDirectories));

        await File.WriteAllTextAsync(statePath, "{");
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(initial.RunId).AsTask());

        var second = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(second);
        var snapshotPath = Path.Combine(repository.Value, Assert.IsType<PathArtifactRef>(second.DefinitionSnapshotRef).Path.Value);
        await File.WriteAllTextAsync(snapshotPath, "{\"schemaVersion\":999}");
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(second.RunId).AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_RejectsStateDefinitionDigestThatDiffersFromSnapshot ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "digest-corruption");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var statePath = Assert.Single(Directory.GetFiles(repository.Value, "state.json", SearchOption.AllDirectories));
        var json = await File.ReadAllTextAsync(statePath);
        await File.WriteAllTextAsync(statePath, json.Replace(DefinitionDigest.ToString(), new string('f', 64), StringComparison.Ordinal));

        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(initial.RunId).AsTask());
    }

    [Theory]
    [InlineData("digest")]
    [InlineData("size")]
    [Trait("Size", "Medium")]
    public async Task Read_RejectsDefinitionSnapshotReferenceDigestOrSizeMismatch (string mismatch)
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", $"snapshot-reference-{mismatch}");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        Assert.NotNull(await store.ReadAsync(initial.RunId));

        var statePath = await FindStatePathAsync(repository, initial.RunId);
        var state = JsonNode.Parse(await File.ReadAllTextAsync(statePath))!.AsObject();
        var reference = state["definitionSnapshotRef"]!.AsObject();
        if (mismatch == "digest")
        {
            reference["digest"] = new string('f', 64);
        }
        else
        {
            reference["sizeBytes"] = reference["sizeBytes"]!.GetValue<long>() + 1;
        }
        var tampered = state.ToJsonString();
        Assert.NotNull(JsonSerializer.Deserialize<ProgramRunRecord>(tampered, IpcJsonSerializerOptions.Default));
        await File.WriteAllTextAsync(statePath, tampered);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(initial.RunId).AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadDefinitionAsync_RestoresAnIndependentlyAuthoredClosedSnapshotArtifact ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "independent-snapshot");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var fixture = """
            {"definitionDigest":"14c934ffaac9d7cfce1bcda1de4d74cfbc14d35d8f3eae8d119dfb2e84c5c629","program":{"steps":[{"command":"ready","timeoutMilliseconds":1000}]},"sourceManifest":{"digest":"ad9deb8f7f2628012c4f15ffd29a79892ddceaa9237b530951f5b8aad33b60be","rootSource":0,"rootPath":null,"presetId":null,"programDigest":"7122109bf0b4b7b10dab6e76b8f9b57d7532d1f0e010ae0215366d78b3e23e28","sources":[]},"sources":[]}
            """;
        var bytes = Encoding.UTF8.GetBytes(fixture);
        var snapshot = Assert.IsType<PathArtifactRef>(initial.DefinitionSnapshotRef);
        await File.WriteAllBytesAsync(Path.Combine(repository.Value, snapshot.Path.Value), bytes);
        var statePath = await FindStatePathAsync(repository, initial.RunId);
        var state = JsonNode.Parse(await File.ReadAllTextAsync(statePath))!.AsObject();
        var reference = state["definitionSnapshotRef"]!.AsObject();
        reference["digest"] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        reference["sizeBytes"] = bytes.Length;
        await File.WriteAllTextAsync(statePath, state.ToJsonString());

        var stored = await store.ReadDefinitionAsync(initial.RunId);
        Assert.NotNull(stored);
        var step = Assert.IsType<ReadyProgramStep>(Assert.Single(stored.Definition.Steps));
        Assert.Equal(1000, step.TimeoutMilliseconds);
        Assert.Equal(ProgramRootSource.Stdin, stored.Definition.SourceManifest.RootSource);
        Assert.Empty(stored.Definition.SourceManifest.Sources);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_RejectsCancellationFactsAddedToAnUnrequestedRunJson ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "cancellation-json");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var statePath = Assert.Single(Directory.GetFiles(repository.Value, "state.json", SearchOption.AllDirectories));
        var json = JsonNode.Parse(await File.ReadAllTextAsync(statePath))!.AsObject();
        json["cancellation"]!.AsObject()["requestedAtUtc"] = StartedAtUtc.ToString("O");
        await File.WriteAllTextAsync(statePath, json.ToJsonString());

        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(initial.RunId).AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_RejectsUnknownSnapshotSchemaAfterReferenceIsRetargetedToTamperedBytes ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "snapshot-schema");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var tamperedBytes = System.Text.Encoding.UTF8.GetBytes("{\"schemaVersion\":999}");
        var snapshot = Assert.IsType<PathArtifactRef>(initial.DefinitionSnapshotRef);
        var snapshotPath = Path.Combine(repository.Value, snapshot.Path.Value);
        await File.WriteAllBytesAsync(snapshotPath, tamperedBytes);
        var statePath = Assert.Single(Directory.GetFiles(repository.Value, "state.json", SearchOption.AllDirectories));
        var state = await File.ReadAllTextAsync(statePath);
        var digest = Convert.ToHexString(SHA256.HashData(tamperedBytes)).ToLowerInvariant();
        state = state.Replace(snapshot.Digest.ToString(), digest, StringComparison.Ordinal)
            .Replace($"\"sizeBytes\":{snapshot.SizeBytes}", $"\"sizeBytes\":{tamperedBytes.Length}", StringComparison.Ordinal);
        await File.WriteAllTextAsync(statePath, state);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(initial.RunId).AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadDefinitionAsync_RejectsAnArtifactPropertyOutsideTheClosedSnapshotContract ()
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "read-definition-tamper");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var snapshot = Assert.IsType<PathArtifactRef>(initial.DefinitionSnapshotRef);
        var snapshotPath = Path.Combine(repository.Value, snapshot.Path.Value);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(snapshotPath))!.AsObject();
        json["extra"] = true;
        var tamperedBytes = System.Text.Encoding.UTF8.GetBytes(json.ToJsonString());
        await File.WriteAllBytesAsync(snapshotPath, tamperedBytes);

        var statePath = Assert.Single(Directory.GetFiles(repository.Value, "state.json", SearchOption.AllDirectories));
        var state = await File.ReadAllTextAsync(statePath);
        var digest = Convert.ToHexString(SHA256.HashData(tamperedBytes)).ToLowerInvariant();
        await File.WriteAllTextAsync(statePath, state.Replace(snapshot.Digest.ToString(), digest, StringComparison.Ordinal)
            .Replace($"\"sizeBytes\":{snapshot.SizeBytes}", $"\"sizeBytes\":{tamperedBytes.Length}", StringComparison.Ordinal));

        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadDefinitionAsync(initial.RunId).AsTask());
    }

    [Theory]
    [InlineData("\"command\":\"ready\"", "\"command\":\"unknown\"")]
    [InlineData("\"timeoutMilliseconds\":1000", "\"timeoutMilliseconds\":1001")]
    [Trait("Size", "Medium")]
    public async Task Read_RejectsRetargetedSnapshotWithUnknownKindOrTypedCanonicalMismatch (string original, string replacement)
    {
        using var scope = TestDirectories.CreateTempScope("program-run-store", "snapshot-content");
        var repository = AbsolutePath.Parse(scope.GetPath("repository"));
        var store = new FileProgramRunStore(repository, CreateProject().ProjectFingerprint);
        var initial = await CreateRunAsync(store, Guid.NewGuid(), 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);

        var snapshot = Assert.IsType<PathArtifactRef>(initial.DefinitionSnapshotRef);
        var snapshotPath = Path.Combine(repository.Value, snapshot.Path.Value);
        var originalBytes = await File.ReadAllBytesAsync(snapshotPath);
        var tamperedText = System.Text.Encoding.UTF8.GetString(originalBytes).Replace(original, replacement, StringComparison.Ordinal);
        Assert.NotEqual(System.Text.Encoding.UTF8.GetString(originalBytes), tamperedText);
        var tamperedBytes = System.Text.Encoding.UTF8.GetBytes(tamperedText);
        await File.WriteAllBytesAsync(snapshotPath, tamperedBytes);

        var statePath = Assert.Single(Directory.GetFiles(repository.Value, "state.json", SearchOption.AllDirectories));
        var state = await File.ReadAllTextAsync(statePath);
        var digest = Convert.ToHexString(SHA256.HashData(tamperedBytes)).ToLowerInvariant();
        await File.WriteAllTextAsync(statePath, state.Replace(snapshot.Digest.ToString(), digest, StringComparison.Ordinal)
            .Replace($"\"sizeBytes\":{snapshot.SizeBytes}", $"\"sizeBytes\":{tamperedBytes.Length}", StringComparison.Ordinal));

        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync(initial.RunId).AsTask());
    }

    private static ProgramRunRecord CreateRun (
        Guid runId,
        long version,
        ProgramRunState state,
        int cursor,
        ProgramCancellationRecord cancellation,
        ExecutionApplicationState applicationState = ExecutionApplicationState.NotApplied,
        ProgramRunFixedContext? fixedContext = null,
        ArtifactRef? terminalRecordRef = null,
        ProgramStepState? stepStateOverride = null,
        ArtifactRef? stepTerminalRecordRef = null,
        DateTimeOffset? planningStartedAtUtc = null,
        ProgramRunStepRecord? stepOverride = null,
        LifecycleExecutionHostRegistration? hostOverride = null,
        ArtifactRef? definitionSnapshotRef = null,
        UnityProjectIdentity? projectOverride = null,
        Sha256Digest? definitionDigest = null,
        IReadOnlyList<ProgramRunStepRecord>? stepsOverride = null)
    {
        var stepState = stepStateOverride ?? (state == ProgramRunState.Created ? ProgramStepState.Deferred : ProgramStepState.Planning);
        return new ProgramRunRecord(
            ProgramRunRecord.CurrentSchemaVersion,
            version,
            runId,
            definitionDigest ?? DefinitionDigest,
            definitionSnapshotRef ?? throw new ArgumentNullException(nameof(definitionSnapshotRef)),
            projectOverride ?? CreateProject(),
            fixedContext ?? CreateFixedContext(),
            hostOverride ?? new LifecycleExecutionHostRegistration(new ProcessIdentity(101, 1), Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("10000000-0000-0000-0000-000000000002")),
            new UnityEditorGenerationSnapshot(1, 2, 3, 4),
            null,
            StartedAtUtc.AddMinutes(5),
            StartedAtUtc,
            StartedAtUtc.AddSeconds(version),
            state,
            cursor,
            stepsOverride ?? [stepOverride ?? new ProgramRunStepRecord("ready", 1000, stepState, null, stepState == ProgramStepState.Deferred ? null : planningStartedAtUtc ?? StartedAtUtc, stepState == ProgramStepState.Deferred ? null : StartedAtUtc.AddSeconds(10), null, null, applicationState, null, [], null, null, null, stepTerminalRecordRef, null, [], null, null, ProgramRunStateSemantics.IsTerminal(stepState) ? StartedAtUtc.AddSeconds(10) : null)],
            [],
            cancellation,
            terminalRecordRef);
    }

    private static async ValueTask<(ProgramRunRecord Run, ArtifactRef StepTerminalRef)> PublishTerminalStepAsync (
        FileProgramRunStore store,
        Guid runId,
        string errorCode)
    {
        var initial = await CreateRunAsync(store, runId, 0, ProgramRunState.Running, 0, ProgramCancellationRecord.None);
        await store.CreateAsync(initial);
        var publication = await store.PublishStepTerminalAsync(initial, 0, CreateStepTerminal(initial, errorCode),
            artifact => CreateStepTerminalReplacement(initial, artifact, errorCode));
        return (publication.Current, publication.TerminalRecordRef);
    }

    private static async ValueTask<(ProgramRunRecord Run, ArtifactRef StepTerminalRef, ArtifactRef RunTerminalRef)> PublishTerminalRunAsync (
        FileProgramRunStore store,
        Guid runId,
        string errorCode)
    {
        var step = await PublishTerminalStepAsync(store, runId, errorCode);
        var terminal = CreateRunTerminal(step.Run, step.StepTerminalRef, errorCode);
        var publication = await store.PublishRunTerminalAsync(step.Run, terminal,
            artifact => CreateTerminalRunReplacement(step.Run, artifact, step.StepTerminalRef, errorCode));
        return (publication.Current, step.StepTerminalRef, publication.TerminalRecordRef);
    }

    private static async ValueTask<string> FindStatePathAsync (AbsolutePath repository, Guid runId)
    {
        foreach (var statePath in Directory.GetFiles(repository.Value, "state.json", SearchOption.AllDirectories))
        {
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(statePath));
            if (document.RootElement.GetProperty("runId").GetGuid() == runId)
            {
                return statePath;
            }
        }
        throw new InvalidOperationException("Program Run state file was not found.");
    }

    private static ProgramRunRecord CreateRunRecord (int cursor, ProgramRunState state, IReadOnlyList<ProgramRunStepRecord> steps, ArtifactRef snapshot, ArtifactRef? terminal = null) => new(
        ProgramRunRecord.CurrentSchemaVersion, 0, Guid.NewGuid(), DefinitionDigest, snapshot, CreateProject(), CreateFixedContext(),
        new LifecycleExecutionHostRegistration(new ProcessIdentity(101, 1), Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("10000000-0000-0000-0000-000000000002")),
        new UnityEditorGenerationSnapshot(1, 2, 3, 4), null, StartedAtUtc.AddMinutes(5), StartedAtUtc, StartedAtUtc, state, cursor, steps, [], ProgramCancellationRecord.None, terminal);

    private static async ValueTask<ProgramRunRecord> CreateRunAsync (
        FileProgramRunStore store, Guid runId, long version, ProgramRunState state, int cursor, ProgramCancellationRecord cancellation)
    {
        using var program = System.Text.Json.JsonDocument.Parse("{\"steps\":[{\"command\":\"ready\",\"timeoutMilliseconds\":1000}]}");
        var snapshot = new ProgramDefinitionSnapshot(
            DefinitionDigest,
            program.RootElement.Clone(),
            new ProgramDefinitionSnapshotManifest(
                Sha256Digest.Parse("ad9deb8f7f2628012c4f15ffd29a79892ddceaa9237b530951f5b8aad33b60be"),
                ProgramRootSource.Stdin,
                null,
                null,
                Sha256Digest.Parse("7122109bf0b4b7b10dab6e76b8f9b57d7532d1f0e010ae0215366d78b3e23e28"),
                []),
            []);
        var reference = await store.PublishDefinitionSnapshotAsync(runId, snapshot);
        return CreateRun(runId, version, state, cursor, cancellation, definitionSnapshotRef: reference);
    }

    private static async ValueTask<(ProgramRunRecord Run, ActiveExecutionRef ActiveReference, TerminalExecutionRef TerminalReference)> CreateActiveRefreshRunAsync (
        FileProgramRunStore store)
    {
        var runId = Guid.NewGuid();
        using var program = JsonDocument.Parse("{\"steps\":[{\"command\":\"refresh\",\"timeoutMilliseconds\":1000}]}");
        var programDigest = Sha256Digest.Compute(Rfc8785JsonCanonicalizer.Canonicalize(program.RootElement));
        using var manifest = JsonDocument.Parse($"{{\"rootSource\":\"stdin\",\"rootPath\":null,\"presetId\":null,\"programDigest\":\"{programDigest}\",\"sources\":[]}}");
        var manifestDigest = Sha256Digest.Compute(Rfc8785JsonCanonicalizer.Canonicalize(manifest.RootElement));
        using var identity = JsonDocument.Parse($"{{\"program\":{program.RootElement.GetRawText()},\"sources\":[]}}");
        var definitionDigest = Sha256Digest.Compute(Rfc8785JsonCanonicalizer.Canonicalize(identity.RootElement));
        var definition = new ProgramDefinitionSnapshot(
            definitionDigest,
            program.RootElement.Clone(),
            new ProgramDefinitionSnapshotManifest(manifestDigest, ProgramRootSource.Stdin, null, null, programDigest, []),
            []);
        var definitionRef = await store.PublishDefinitionSnapshotAsync(runId, definition);
        var fixedContext = CreateFixedContext(commandTimeouts: new Dictionary<string, int> { ["refresh"] = 1000 });
        var deferred = new ProgramRunStepRecord("refresh", 1000, ProgramStepState.Deferred, null, null, null, null, null,
            ExecutionApplicationState.NotApplied, null, [], null, null, null, null, null, [], null, null, null);
        var initial = CreateRun(runId, 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None,
            fixedContext: fixedContext, definitionSnapshotRef: definitionRef, definitionDigest: definitionDigest, stepOverride: deferred);
        await store.CreateAsync(initial);

        var execution = new ProgramStepExecutionReference(Guid.Parse("3a0f9ada-c23f-41b7-88ac-849cedf38882"), StartedAtUtc, StartedAtUtc.AddSeconds(10));
        var planning = deferred with
        {
            State = ProgramStepState.Planning,
            PlanningStartedAtUtc = StartedAtUtc,
            DeadlineUtc = execution.DeadlineUtc,
            StartedAtUtc = execution.StartedAtUtc,
            Execution = execution,
            ExecutionPortInvoked = true,
        };
        var plannedRun = CreateRun(runId, 1, ProgramRunState.Running, 0, ProgramCancellationRecord.None,
            fixedContext: fixedContext, definitionSnapshotRef: definitionRef, definitionDigest: definitionDigest, stepOverride: planning);
        Assert.True((await store.CompareExchangeAsync(initial, plannedRun)).Exchanged);

        var activeReference = CreateRefreshActiveReference(execution.ExecutionId);
        var activeStep = planning with
        {
            State = ProgramStepState.Running,
            GenerationBefore = plannedRun.StartedGeneration,
            LifecycleExecutionRef = activeReference,
        };
        var activeRun = CreateRun(runId, 2, ProgramRunState.Running, 0, ProgramCancellationRecord.None,
            fixedContext: fixedContext, definitionSnapshotRef: definitionRef, definitionDigest: definitionDigest, stepOverride: activeStep);
        Assert.True((await store.CompareExchangeAsync(plannedRun, activeRun)).Exchanged);
        return (activeRun, activeReference, CreateRefreshTerminalReference(execution.ExecutionId));
    }

    private static ProgramStepTerminalRecord CreateRefreshStepTerminal (ProgramRunRecord run, TerminalExecutionRef terminalReference) => new(
        ProgramStepTerminalRecord.CurrentSchemaVersion,
        run.RunId,
        run.DefinitionDigest,
        0,
        "refresh",
        ProgramStepState.Completed,
        null,
        ExecutionApplicationState.Applied,
        run.StartedGeneration,
        run.StartedGeneration,
        null,
        [],
        terminalReference,
        null,
        [],
        null,
        StartedAtUtc,
        StartedAtUtc.AddSeconds(1));

    private static ActiveExecutionRef CreateRefreshActiveReference (Guid executionId)
    {
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        return new ActiveExecutionRef(
            definition.ExecutionKind,
            executionId,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new ExecutionState("registered"),
            new ExecutionStatusLocator($"lifecycle-execution/refresh/{executionId:N}/execution.json"));
    }

    private static TerminalExecutionRef CreateRefreshTerminalReference (Guid executionId)
    {
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        return new TerminalExecutionRef(
            definition.ExecutionKind,
            executionId,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new ExecutionState("completed"),
            null,
            new PathArtifactRef(
                LifecycleExecutionArtifactContract.TerminalRecordKind,
                LifecycleExecutionArtifactContract.TerminalRecordMediaType,
                new ArtifactPath($"lifecycle-execution/refresh/{executionId:N}/terminal.json"),
                Sha256Digest.Parse(new string('e', 64)),
                1,
                StartedAtUtc));
    }

    private static async ValueTask<(ProgramRunRecord Initial, ProgramRunStepRecord Planning, ArtifactRef Plan, IReadOnlyList<ArtifactRef> Descriptors)> CreateCallPlanningRunAsync (FileProgramRunStore store)
    {
        var runId = Guid.NewGuid();
        using var program = JsonDocument.Parse("{\"steps\":[{\"command\":\"call\",\"timeoutMilliseconds\":1000,\"steps\":[]}]} ");
        var programDigest = Sha256Digest.Compute(Rfc8785JsonCanonicalizer.Canonicalize(program.RootElement));
        using var manifest = JsonDocument.Parse($"{{\"rootSource\":\"stdin\",\"rootPath\":null,\"presetId\":null,\"programDigest\":\"{programDigest}\",\"sources\":[]}}");
        var manifestDigest = Sha256Digest.Compute(Rfc8785JsonCanonicalizer.Canonicalize(manifest.RootElement));
        using var identity = JsonDocument.Parse($"{{\"program\":{program.RootElement.GetRawText()},\"sources\":[]}}");
        var definitionDigest = Sha256Digest.Compute(Rfc8785JsonCanonicalizer.Canonicalize(identity.RootElement));
        var snapshot = new ProgramDefinitionSnapshot(
            definitionDigest,
            program.RootElement.Clone(),
            new ProgramDefinitionSnapshotManifest(manifestDigest, ProgramRootSource.Stdin, null, null, programDigest, []),
            []);
        var snapshotRef = await store.PublishDefinitionSnapshotAsync(runId, snapshot);
        var host = CreateHost();
        var generation = new UnityEditorGenerationSnapshot(1, 2, 3, 4);
        var plan = CreateArtifact("requestPlan", "call-plan.json");
        var descriptors = new[] { CreateArtifact("operationDescriptor", "one.json"), CreateArtifact("operationDescriptor", "two.json") };
        var boundary = new ProgramRequestExecutionBoundary(Guid.NewGuid(), CreateProject(), host, generation, Sha256Digest.Compute([1]), plan, null, descriptors, [Sha256Digest.Compute([2]), Sha256Digest.Compute([3])], StartedAtUtc, StartedAtUtc.AddSeconds(10));
        var deferred = new ProgramRunStepRecord("call", 1000, ProgramStepState.Deferred, null, null, null, null, null,
            ExecutionApplicationState.NotApplied, null, [], null, null, null, null, null, [], null, null, null);
        var initial = CreateRun(runId, 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None,
            definitionSnapshotRef: snapshotRef, hostOverride: host, stepOverride: deferred, definitionDigest: definitionDigest);
        var planning = deferred with
        {
            State = ProgramStepState.Planning,
            PlanningStartedAtUtc = StartedAtUtc,
            DeadlineUtc = StartedAtUtc.AddSeconds(10),
            GenerationBefore = generation,
            RequestPlanRef = plan,
            OperationDescriptorRefs = descriptors,
            RequestExecution = boundary,
            StartedAtUtc = StartedAtUtc,
        };
        return (initial, planning, plan, descriptors);
    }

    private static async ValueTask<ProgramRunRecord> CreateImplicitReadyRunAsync (
        FileProgramRunStore store,
        Guid runId,
        int timeoutMilliseconds,
        IReadOnlyDictionary<string, int> commandTimeouts)
    {
        using var program = JsonDocument.Parse("{\"steps\":[{\"command\":\"ready\"}]}");
        var programDigest = Sha256Digest.Compute(Rfc8785JsonCanonicalizer.Canonicalize(program.RootElement));
        using var manifest = JsonDocument.Parse($"{{\"rootSource\":\"stdin\",\"rootPath\":null,\"presetId\":null,\"programDigest\":\"{programDigest}\",\"sources\":[]}}");
        var manifestDigest = Sha256Digest.Compute(Rfc8785JsonCanonicalizer.Canonicalize(manifest.RootElement));
        using var definitionIdentity = JsonDocument.Parse($"{{\"program\":{program.RootElement.GetRawText()},\"sources\":[]}}");
        var definitionDigest = Sha256Digest.Compute(Rfc8785JsonCanonicalizer.Canonicalize(definitionIdentity.RootElement));
        var snapshot = new ProgramDefinitionSnapshot(
            definitionDigest,
            program.RootElement.Clone(),
            new ProgramDefinitionSnapshotManifest(
                manifestDigest,
                ProgramRootSource.Stdin,
                null,
                null,
                programDigest,
                []),
            []);
        var reference = await store.PublishDefinitionSnapshotAsync(runId, snapshot);
        return CreateRun(runId, 0, ProgramRunState.Created, 0, ProgramCancellationRecord.None,
            fixedContext: CreateFixedContext(commandTimeouts: commandTimeouts), definitionSnapshotRef: reference, definitionDigest: definitionDigest,
            stepOverride: new ProgramRunStepRecord("ready", timeoutMilliseconds, ProgramStepState.Deferred, null, null, null, null, null,
                ExecutionApplicationState.NotApplied, null, [], null, null, null, null, null, [], null, null, null));
    }

    private static ProgramRunStepRecord CreateFailedStep (ArtifactRef terminalRecordRef) => new(
        "ready", 1000, ProgramStepState.Failed, null, StartedAtUtc, StartedAtUtc.AddSeconds(10), null, null,
        ExecutionApplicationState.NotApplied, null, [], null, null, null, terminalRecordRef, null, [], null, null, StartedAtUtc.AddSeconds(10));

    private static ProgramRunTerminalRecord CreateRunTerminal (ProgramRunRecord initial, ArtifactRef stepTerminalRef, string errorCode) => new(
        ProgramRunTerminalRecord.CurrentSchemaVersion, CreateProject(), initial.RunId, initial.DefinitionDigest,
        initial.DefinitionSnapshotRef, initial.DeadlineUtc, CreateSnapshotManifest(), initial.FixedContext, ProgramRunState.Failed, null,
        ExecutionApplicationState.NotApplied,
        [CreateFailedStep(stepTerminalRef) with { ErrorCode = errorCode }], [], ProgramCancellationRecord.None,
        initial.CurrentEditorGeneration, initial.StartedAtUtc, StartedAtUtc.AddSeconds(initial.Version + 1))
    {
        FinalSupervisorSnapshot = initial.FixedContext.Supervisor,
    };

    private static ProgramRunRecord CreateTerminalRunReplacement (ProgramRunRecord initial, ArtifactRef terminalRef, ArtifactRef stepTerminalRef, string errorCode) =>
        CreateRun(initial.RunId, initial.Version + 1, ProgramRunState.Failed, 0, ProgramCancellationRecord.None,
            terminalRecordRef: terminalRef, stepOverride: CreateFailedStep(stepTerminalRef) with { ErrorCode = errorCode },
            definitionSnapshotRef: initial.DefinitionSnapshotRef);

    private static ProgramStepTerminalRecord CreateStepTerminal (ProgramRunRecord initial, string errorCode) => new(
        ProgramStepTerminalRecord.CurrentSchemaVersion, initial.RunId, initial.DefinitionDigest, 0, "ready",
        ProgramStepState.Failed, null, ExecutionApplicationState.NotApplied, null, null, null, [], null, null, [], errorCode,
        null, StartedAtUtc.AddSeconds(10));

    private static ProgramRunRecord CreateStepTerminalReplacement (ProgramRunRecord initial, ArtifactRef terminalRef, string errorCode) =>
        CreateRun(initial.RunId, initial.Version + 1, ProgramRunState.Running, 0, ProgramCancellationRecord.None,
            stepOverride: CreateFailedStep(terminalRef) with { ErrorCode = errorCode },
            definitionSnapshotRef: initial.DefinitionSnapshotRef);

    private static UnityProjectIdentity CreateProject () => new(ProjectPathTestValues.WorkspaceUnityProject, new ProjectFingerprint(new string('b', 64)), "6000.1.0f1");

    private static ResolvedUnityProjectContext CreateResolvedProject () => ResolvedUnityProjectContext.Create(
        AbsolutePath.Parse(ProjectPathTestValues.WorkspaceUnityProject), AbsolutePath.Parse(ProjectPathTestValues.WorkspaceRoot), CreateProject().ProjectFingerprint,
        UnityProjectPathSource.CurrentDirectory, null, "6000.1.0f1");

    private sealed class SingleStoreFactory (IProgramRunStore store) : IProgramRunStoreFactory
    {
        public IProgramRunStore ForProject (ResolvedUnityProjectContext project) => store;
    }

    private sealed class FixedGuidGenerator (Guid value) : IGuidGenerator
    {
        public Guid Generate () => value;
    }

    private sealed class MatchingObserver : IProcessIdentityObserver
    {
        public ProcessIdentityStatus Observe (ProcessIdentity process) => ProcessIdentityStatus.Matching;
    }

    private static ProgramRunFixedContext CreateFixedContext (bool allowDangerous = true, IReadOnlyDictionary<string, int>? commandTimeouts = null)
    {
        var timeouts = commandTimeouts ?? new Dictionary<string, int> { ["ready"] = 1000 };
        return new(
        new ProgramEffectiveAuthorizationSnapshot(allowDangerous, false, IpcProgramEffectiveAuthorizationSnapshot.ComputeDigest(allowDangerous, false).ToString(), StartedAtUtc),
        new ProgramEffectiveConfigurationSnapshot(1, OperationPolicy.Safe, PlanTokenMode.Optional, ReadIndexMode.RequireFresh, ["^ucli\\."], 1000, timeouts,
            IpcProgramEffectiveConfigurationSnapshot.ComputeDigest(1, "safe", "optional", "requireFresh", ["^ucli\\."], 1000, timeouts), StartedAtUtc),
        new ProgramExecutionModeSnapshot("auto", "daemon"),
        new ProgramAttachedSupervisorSnapshot(Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("10000000-0000-0000-0000-000000000004"), new ProcessIdentity(42, 100), ProgramSupervisorConnection.Connected, ProgramSupervisorAvailability.Available, StartedAtUtc));
    }

    private static ProgramDefinitionSnapshotManifest CreateSnapshotManifest () => new(
        Sha256Digest.Parse("ad9deb8f7f2628012c4f15ffd29a79892ddceaa9237b530951f5b8aad33b60be"), ProgramRootSource.Stdin,
        null, null, Sha256Digest.Parse("7122109bf0b4b7b10dab6e76b8f9b57d7532d1f0e010ae0215366d78b3e23e28"), []);

    private static ProgramRunStepRecord CreatePlanningStep (ExecutionApplicationState applicationState) => CreatePlanningStep("ready", applicationState);

    private static ProgramRunStepRecord CreatePlanningStep (string command, ExecutionApplicationState applicationState = ExecutionApplicationState.NotApplied) => new(
        command, 1000, ProgramStepState.Planning, null, StartedAtUtc, StartedAtUtc.AddSeconds(10), null, null,
        applicationState, null, [], null, null, null, null, null, [], null, null, null);

    private static ExecutionRef CreateLifecycleReference (LifecycleExecutionKind kind) => new ActiveExecutionRef(
        new ExecutionKind(TextVocabulary.GetText(kind)), Guid.NewGuid(), DefinitionDigest, new ExecutionState("running"), new ExecutionStatusLocator("status"));

    private static ProgramRequestExecutionBoundary CreateRequestExecutionBoundary (
        LifecycleExecutionHostRegistration? host = null,
        UnityEditorGenerationSnapshot? generation = null) => new(
        Guid.NewGuid(), CreateProject(), host ?? CreateHost(), generation ?? new UnityEditorGenerationSnapshot(1, 2, 3, 4),
        Sha256Digest.Compute([1]), CreateArtifact("requestPlan", "plan.json"), null, [], [], StartedAtUtc, StartedAtUtc.AddSeconds(10));

    private static LifecycleExecutionHostRegistration CreateHost (int processId = 101) => new(
        new ProcessIdentity(processId, 1), Guid.Parse("10000000-0000-0000-0000-000000000001"),
        Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("10000000-0000-0000-0000-000000000002"));

    private static ArtifactRef CreateArtifact (string kind, string path) => new PathArtifactRef(
        new ArtifactKind(kind),
        new ArtifactMediaType("application/json"),
        new ArtifactPath($"artifacts/{path}"),
        Sha256Digest.Parse(new string('c', 64)),
        1,
        StartedAtUtc);

    private static PathArtifactRef CreateArtifactReference (PathArtifactRef source, Sha256Digest digest, ulong sizeBytes) => new(
        source.Kind, source.MediaType, source.Path, digest, sizeBytes, source.CreatedAtUtc);
}
