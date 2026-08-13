using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Application.Features.Programs.Supervision;
using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Features.Screenshot.Capture;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Features.Programs.Supervision;

public sealed class ProgramLifecycleStepExecutionPortTests
{
    private static readonly DateTimeOffset StartedAtUtc = new(2026, 8, 12, 22, 34, 39, TimeSpan.Zero);
    private static readonly UnityEditorGenerationSnapshot Generation = new(0, 0, 2, 0);

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartAsync_WhenRefreshReturnsTerminalAfterObserverCas_ProjectsThePersistedSameIdentity ()
    {
        var project = ProjectContextTestFactory.Create();
        var execution = new ProgramStepExecutionReference(Guid.Parse("3a0f9ada-c23f-41b7-88ac-849cedf38882"), StartedAtUtc, StartedAtUtc.AddMinutes(3));
        var run = CreateRun(project, execution);
        var activeReference = CreateActiveReference(execution.ExecutionId);
        var terminalReference = CreateTerminalReference(execution.ExecutionId);
        var terminalRecord = CreateCompletedRefreshTerminal(project.UnityProject, run.Host, execution.ExecutionId);
        var store = new MemoryStore(run);
        var refresh = new ObservingRefreshService(activeReference, terminalReference, project.UnityProject, run.Host);
        var port = CreatePort(project, run.Host, store, refresh, new RecordingLifecycleExecutionReconnectResolver(
            new LifecycleExecutionReconnectResolution.Terminal(terminalReference, terminalRecord)));

        var result = await port.StartAsync(new ProgramStepExecutionStart(run, 0, execution));

        var terminal = Assert.IsType<ProgramStepExecutionRecoveredTerminal>(result.Terminal);
        Assert.Equal(ProgramStepState.Completed, terminal.State);
        Assert.Equal(ExecutionApplicationState.Applied, terminal.ApplicationState);
        Assert.Equal(Generation, terminal.GenerationAfter);
        Assert.Equal(terminalReference, terminal.LifecycleExecutionRef);
        Assert.Equal(ProgramStepState.Running, store.Current.Steps[0].State);
        Assert.Equal(activeReference, store.Current.Steps[0].LifecycleExecutionRef);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartAsync_WhenReturnedTerminalDoesNotMatchPersistedLifecycleIdentity_InterruptsProjection ()
    {
        var project = ProjectContextTestFactory.Create();
        var execution = new ProgramStepExecutionReference(Guid.Parse("3a0f9ada-c23f-41b7-88ac-849cedf38882"), StartedAtUtc, StartedAtUtc.AddMinutes(3));
        var run = CreateRun(project, execution);
        var activeReference = CreateActiveReference(execution.ExecutionId);
        var unexpectedTerminal = CreateTerminalReference(Guid.Parse("a3c36851-89a4-49aa-9f99-1ea5d335b44b"));
        var store = new MemoryStore(run);
        var refresh = new ObservingRefreshService(activeReference, unexpectedTerminal, project.UnityProject, run.Host);
        var port = CreatePort(project, run.Host, store, refresh, new RecordingLifecycleExecutionReconnectResolver(
            new LifecycleExecutionReconnectResolution.Terminal(
                unexpectedTerminal,
                CreateCompletedRefreshTerminal(project.UnityProject, run.Host, unexpectedTerminal.Id))));

        var result = await port.StartAsync(new ProgramStepExecutionStart(run, 0, execution));

        var terminal = Assert.IsType<ProgramStepExecutionRecoveredTerminal>(result.Terminal);
        Assert.Equal(ProgramStepState.Interrupted, terminal.State);
        Assert.Equal("PROGRAM_TERMINAL_PROJECTION_MISMATCH", terminal.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ProgramRequestGenerationMismatch_IsInterruptedBeforeGenerationEqualityValidation ()
    {
        var execution = new ProgramStepExecutionReference(Guid.NewGuid(), StartedAtUtc, StartedAtUtc.AddMinutes(3));
        var run = CreateCallRun(ProjectContextTestFactory.Create(), execution);
        var response = new IpcProgramRequestExecutionResponse(
            IpcProgramRequestExecutionStatus.GenerationMismatch,
            execution.ExecutionId,
            run.Host,
            new UnityEditorGenerationSnapshot(99, 99, 99, 99));

        var terminal = await InvokeCallTerminalAsync(run, execution.ExecutionId, response);

        Assert.Equal(ProgramStepState.Interrupted, terminal!.State);
        Assert.Equal("PROGRAM_GENERATION_MISMATCH", terminal.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ProgramRequestTerminalWithUnexpectedGeneration_IsContractInvalid ()
    {
        var execution = new ProgramStepExecutionReference(Guid.NewGuid(), StartedAtUtc, StartedAtUtc.AddMinutes(3));
        var run = CreateCallRun(ProjectContextTestFactory.Create(), execution);
        var response = new IpcProgramRequestExecutionResponse(
            IpcProgramRequestExecutionStatus.Terminal,
            execution.ExecutionId,
            run.Host,
            new UnityEditorGenerationSnapshot(99, 99, 99, 99),
            [1]);

        var terminal = await InvokeCallTerminalAsync(run, execution.ExecutionId, response);

        Assert.Equal(ProgramStepState.Interrupted, terminal!.State);
        Assert.Equal("PROGRAM_CALL_RESPONSE_CONTRACT_INVALID", terminal.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadyNotReady_ReturnsCompletedFailWithoutError ()
    {
        var project = ProjectContextTestFactory.Create();
        var execution = new ProgramStepExecutionReference(Guid.NewGuid(), StartedAtUtc, StartedAtUtc.AddMinutes(3));
        var source = CreateRun(project, execution);
        var run = ReplaceCommand(source, "ready");
        var store = new MemoryStore(run);
        var port = CreatePort(project, run.Host, store, new ThrowingRefreshService(), new ThrowingLifecycleExecutionReconnectResolver(),
            new FixedReadyService(ProgramReadyObservation.NotReady(Verdict.Fail, Generation)), new MemoryArtifactStoreFactory());

        var result = await port.StartAsync(new ProgramStepExecutionStart(run, 0, execution));

        var terminal = Assert.IsType<ProgramStepExecutionRecoveredTerminal>(result.Terminal);
        Assert.Equal(ProgramStepState.Completed, terminal.State);
        Assert.Equal(Verdict.Fail, terminal.Verdict);
        Assert.Equal(ExecutionApplicationState.NotApplied, terminal.ApplicationState);
        Assert.Null(terminal.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadyObservationFailure_ReturnsInterrupted ()
    {
        var project = ProjectContextTestFactory.Create();
        var execution = new ProgramStepExecutionReference(Guid.NewGuid(), StartedAtUtc, StartedAtUtc.AddMinutes(3));
        var run = ReplaceCommand(CreateRun(project, execution), "ready");
        var store = new MemoryStore(run);
        var port = CreatePort(project, run.Host, store, new ThrowingRefreshService(), new ThrowingLifecycleExecutionReconnectResolver(),
            new FixedReadyService(ProgramReadyObservation.Failed(null, ApplicationFailure.InternalError("unavailable"))), new MemoryArtifactStoreFactory());

        var result = await port.StartAsync(new ProgramStepExecutionStart(run, 0, execution));

        Assert.Equal("PROGRAM_READY_OBSERVATION_UNAVAILABLE", result.Terminal!.ErrorCode);
        Assert.Equal(ProgramStepState.Interrupted, result.Terminal.State);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("inline")]
    [InlineData("requestPath")]
    public async Task CallPreflight_EvalDirectOperationIsRejectedBeforeCatalogAndCall (string source)
    {
        var project = ProjectContextTestFactory.Create();
        var execution = new ProgramStepExecutionReference(Guid.NewGuid(), StartedAtUtc, StartedAtUtc.AddMinutes(3));
        var run = CreateRun(project, execution);
        var resolvedRequest = source switch
        {
            "inline" => """{"protocolVersion":1,"steps":[{"kind":"op","op":"ucli.cs.eval","args":{}}]}""",
            "requestPath" => """{"protocolVersion":1,"steps":[{"kind":"op","op":"ucli.cs.eval","args":{}}]}""",
            _ => throw new ArgumentOutOfRangeException(nameof(source)),
        };
        using var document = JsonDocument.Parse(resolvedRequest);
        var preflight = new ProgramCallPreflightService(
            new ProgramFixedHostCatalogReader(),
            (IRequestStaticValidator)RuntimeHelpers.GetUninitializedObject(typeof(RequestStaticValidator)),
            new ValidateRequestJsonParser(),
            new MemoryArtifactStoreFactory());

        var result = await preflight.PrepareAsync(run, project, new ThrowingBinding(project.UnityProject), document.RootElement, ExecutionDeadline.Start(TimeSpan.FromMinutes(1), new FakeTimeProvider(StartedAtUtc)));

        Assert.False(result.IsSuccess);
        Assert.Equal("PROGRAM_CALL_EVAL_NOT_ALLOWED", result.ErrorCode);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("refresh", false)]
    [InlineData("refresh", true)]
    [InlineData("compile", false)]
    [InlineData("compile", true)]
    [InlineData("play.enter", false)]
    [InlineData("play.enter", true)]
    [InlineData("play.exit", false)]
    [InlineData("play.exit", true)]
    public async Task StartAsync_LifecycleActionsReceiveTheRunFailFastContext (string command, bool failFast)
    {
        var project = ProjectContextTestFactory.Create();
        var execution = new ProgramStepExecutionReference(Guid.NewGuid(), StartedAtUtc, StartedAtUtc.AddMinutes(3));
        var run = ReplaceFixedContext(
            ReplaceCommand(CreateRun(project, execution), command),
            CreateFixedContext() with { FailFast = failFast });
        var refresh = new RecordingRefreshService();
        var compile = new RecordingCompileService();
        var playEnter = new RecordingPlayEnterService();
        var playExit = new RecordingPlayExitService();
        var port = CreatePort(project, run.Host, new MemoryStore(run), refresh,
            new ThrowingLifecycleExecutionReconnectResolver(),
            compileService: compile, playEnterService: playEnter, playExitService: playExit);

        await port.StartAsync(new ProgramStepExecutionStart(run, 0, execution));

        var observedFailFast = command switch
        {
            "refresh" => refresh.FailFast,
            "compile" => compile.Invocation!.Context.FailFast,
            "play.enter" => playEnter.Invocation!.Context.FailFast,
            "play.exit" => playExit.Invocation!.Context.FailFast,
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };
        Assert.Equal(failFast, observedFailFast);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartAsync_WhenLifecycleStartObserverCasConflicts_RetriesTheSameStartWithoutRedispatch ()
    {
        var project = ProjectContextTestFactory.Create();
        var execution = new ProgramStepExecutionReference(Guid.Parse("3a0f9ada-c23f-41b7-88ac-849cedf38882"), StartedAtUtc, StartedAtUtc.AddMinutes(3));
        var run = CreateRun(project, execution);
        var activeReference = CreateActiveReference(execution.ExecutionId);
        var terminalReference = CreateTerminalReference(execution.ExecutionId);
        var store = new MemoryStore(run) { RejectNextCompareExchange = true };
        var refresh = new ObservingRefreshService(activeReference, terminalReference, project.UnityProject, run.Host);
        var port = CreatePort(project, run.Host, store, refresh, new RecordingLifecycleExecutionReconnectResolver(
            new LifecycleExecutionReconnectResolution.Terminal(
                terminalReference,
                CreateCompletedRefreshTerminal(project.UnityProject, run.Host, execution.ExecutionId))));

        var result = await port.StartAsync(new ProgramStepExecutionStart(run, 0, execution));

        Assert.NotNull(result.Terminal);
        Assert.Equal(1, refresh.StartCount);
        Assert.Equal(ProgramStepState.Running, store.Current.Steps[0].State);
        Assert.Equal(activeReference, store.Current.Steps[0].LifecycleExecutionRef);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartAsync_WhenLifecycleTerminalUsesVerifiedSuccessorEndpoint_ProjectsTerminal ()
    {
        var project = ProjectContextTestFactory.Create();
        var execution = new ProgramStepExecutionReference(Guid.Parse("3a0f9ada-c23f-41b7-88ac-849cedf38882"), StartedAtUtc, StartedAtUtc.AddMinutes(3));
        var run = CreateRun(project, execution);
        var activeReference = CreateActiveReference(execution.ExecutionId);
        var terminalReference = CreateTerminalReference(execution.ExecutionId);
        var successorHost = CreateSuccessorHost(run.Host);
        var store = new MemoryStore(run);
        var refresh = new ObservingRefreshService(activeReference, terminalReference, project.UnityProject, run.Host);
        var port = CreatePort(project, run.Host, store, refresh, new RecordingLifecycleExecutionReconnectResolver(
            new LifecycleExecutionReconnectResolution.Terminal(
                terminalReference,
                CreateCompletedRefreshTerminal(project.UnityProject, successorHost, execution.ExecutionId))));

        var result = await port.StartAsync(new ProgramStepExecutionStart(run, 0, execution));

        var terminal = Assert.IsType<ProgramStepExecutionRecoveredTerminal>(result.Terminal);
        Assert.Equal(ProgramStepState.Completed, terminal.State);
        Assert.Equal(terminalReference, terminal.LifecycleExecutionRef);
        Assert.Equal(Generation, terminal.GenerationAfter);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(FixedHostMismatches))]
    public async Task StartAsync_WhenLifecycleTerminalChangesFixedHost_InterruptsProjection (
        LifecycleExecutionHostRegistration terminalHost)
    {
        var project = ProjectContextTestFactory.Create();
        var execution = new ProgramStepExecutionReference(Guid.Parse("3a0f9ada-c23f-41b7-88ac-849cedf38882"), StartedAtUtc, StartedAtUtc.AddMinutes(3));
        var run = CreateRun(project, execution);
        var activeReference = CreateActiveReference(execution.ExecutionId);
        var terminalReference = CreateTerminalReference(execution.ExecutionId);
        var store = new MemoryStore(run);
        var refresh = new ObservingRefreshService(activeReference, terminalReference, project.UnityProject, run.Host);
        var port = CreatePort(project, run.Host, store, refresh, new RecordingLifecycleExecutionReconnectResolver(
            new LifecycleExecutionReconnectResolution.Terminal(
                terminalReference,
                CreateCompletedRefreshTerminal(project.UnityProject, terminalHost, execution.ExecutionId))));

        var result = await port.StartAsync(new ProgramStepExecutionStart(run, 0, execution));

        var terminal = Assert.IsType<ProgramStepExecutionRecoveredTerminal>(result.Terminal);
        Assert.Equal(ProgramStepState.Interrupted, terminal.State);
        Assert.Equal("PROGRAM_TERMINAL_RECORD_INVALID", terminal.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartAsync_WhenReconnectResolverRejectsUnacceptedSuccessorEndpoint_InterruptsProjection ()
    {
        var project = ProjectContextTestFactory.Create();
        var execution = new ProgramStepExecutionReference(Guid.Parse("3a0f9ada-c23f-41b7-88ac-849cedf38882"), StartedAtUtc, StartedAtUtc.AddMinutes(3));
        var run = CreateRun(project, execution);
        var activeReference = CreateActiveReference(execution.ExecutionId);
        var terminalReference = CreateTerminalReference(execution.ExecutionId);
        var store = new MemoryStore(run);
        var refresh = new ObservingRefreshService(activeReference, terminalReference, project.UnityProject, run.Host);
        var port = CreatePort(project, run.Host, store, refresh, new RecordingLifecycleExecutionReconnectResolver(
            new LifecycleExecutionReconnectResolution.Rejected(
                ApplicationFailure.ContractViolation("Lifecycle resolver rejected an unaccepted endpoint successor."))));

        var result = await port.StartAsync(new ProgramStepExecutionStart(run, 0, execution));

        var terminal = Assert.IsType<ProgramStepExecutionRecoveredTerminal>(result.Terminal);
        Assert.Equal(ProgramStepState.Interrupted, terminal.State);
        Assert.Equal("PROGRAM_TERMINAL_RECORD_INVALID", terminal.ErrorCode);
    }

    public static IEnumerable<object[]> FixedHostMismatches ()
    {
        var host = CreateHost();
        yield return [new LifecycleExecutionHostRegistration(
            new ProcessIdentity(32842, 639221708291771771),
            host.EditorInstanceId,
            host.FirstEndpointRegistrationGenerationId,
            host.CurrentEndpointRegistrationGenerationId)];
        yield return [new LifecycleExecutionHostRegistration(
            host.Process,
            Guid.Parse("bad43edd-76ed-4680-9392-f8463dc074b6"),
            host.FirstEndpointRegistrationGenerationId,
            host.CurrentEndpointRegistrationGenerationId)];
        yield return [new LifecycleExecutionHostRegistration(
            new ProcessIdentity(32841, 639221708291771771),
            host.EditorInstanceId,
            host.FirstEndpointRegistrationGenerationId,
            host.CurrentEndpointRegistrationGenerationId)];
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task LifecycleStartObserver_WhenStartUsesVerifiedSuccessorEndpoint_CasRunningStepWithCurrentGeneration ()
    {
        var project = ProjectContextTestFactory.Create();
        var execution = new ProgramStepExecutionReference(Guid.Parse("3a0f9ada-c23f-41b7-88ac-849cedf38882"), StartedAtUtc, StartedAtUtc.AddMinutes(3));
        var expectedGeneration = Generation;
        var run = CreateRun(project, execution);
        var store = new MemoryStore(run);
        var observer = new ProgramLifecycleStartObserver(store, project.UnityProject, new ProgramStepExecutionStart(run, 0, execution));

        var observation = await observer.ObserveAsync(CreateLifecycleStart(
            project.UnityProject,
            CreateSuccessorHost(run.Host),
            CreateActiveReference(execution.ExecutionId),
            expectedGeneration,
            execution));

        Assert.IsType<LifecycleExecutionStartObservation.Observed>(observation);
        Assert.Equal(ProgramStepState.Running, store.Current.Steps[0].State);
        Assert.Equal(expectedGeneration, store.Current.Steps[0].GenerationBefore);
        Assert.Equal(execution.ExecutionId, store.Current.Steps[0].LifecycleExecutionRef!.Id);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(FixedHostMismatches))]
    public async Task LifecycleStartObserver_WhenStartChangesFixedHost_RejectsBeforeCas (
        LifecycleExecutionHostRegistration lifecycleHost)
    {
        var project = ProjectContextTestFactory.Create();
        var execution = new ProgramStepExecutionReference(Guid.Parse("3a0f9ada-c23f-41b7-88ac-849cedf38882"), StartedAtUtc, StartedAtUtc.AddMinutes(3));
        var run = CreateRun(project, execution);
        var store = new MemoryStore(run);
        var observer = new ProgramLifecycleStartObserver(store, project.UnityProject, new ProgramStepExecutionStart(run, 0, execution));

        var observation = await observer.ObserveAsync(CreateLifecycleStart(
            project.UnityProject,
            lifecycleHost,
            CreateActiveReference(execution.ExecutionId),
            Generation,
            execution));

        Assert.IsType<LifecycleExecutionStartObservation.Rejected>(observation);
        Assert.Equal(ProgramStepState.Planning, store.Current.Steps[0].State);
        Assert.Null(store.Current.Steps[0].LifecycleExecutionRef);
    }

    private static ProgramLifecycleStepExecutionPort CreatePort (
        ProjectContext project,
        LifecycleExecutionHostRegistration host,
        MemoryStore store,
        IRefreshService refresh,
        ILifecycleExecutionReconnectResolver reconnectResolver,
        IReadyService? readyService = null,
        IProgramArtifactStoreFactory? artifactStoreFactory = null,
        ICompileService? compileService = null,
        IPlayEnterService? playEnterService = null,
        IPlayExitService? playExitService = null)
    {
        var binding = new ThrowingBinding(project.UnityProject);
        var hostContext = new ProgramRunHostContext(project, binding, host, Generation);
        return new ProgramLifecycleStepExecutionPort(
            hostContext,
            store,
            readyService ?? new ThrowingReadyService(),
            new ThrowingScreenshotService(),
            refresh,
            compileService ?? new ThrowingCompileService(),
            playEnterService ?? new ThrowingPlayEnterService(),
            playExitService ?? new ThrowingPlayExitService(),
            (ProgramCallPreflightService)RuntimeHelpers.GetUninitializedObject(typeof(ProgramCallPreflightService)),
            artifactStoreFactory ?? new ThrowingArtifactStoreFactory(),
            reconnectResolver,
            new FakeTimeProvider(StartedAtUtc));
    }

    private static ProgramRunRecord CreateRun (ProjectContext project, ProgramStepExecutionReference execution)
    {
        var host = CreateHost();
        var step = new ProgramRunPendingStep("refresh", 1_000).ToRecord() with
        {
            State = ProgramStepState.Planning,
            PlanningStartedAtUtc = StartedAtUtc,
            DeadlineUtc = execution.DeadlineUtc,
            StartedAtUtc = execution.StartedAtUtc,
            Execution = execution,
            ExecutionPortInvoked = true,
        };
        return new ProgramRunRecord(
            ProgramRunRecord.CurrentSchemaVersion,
            3,
            Guid.Parse("c8fdb833-f69e-46db-b3b6-c5a0f7c2dca5"),
            Sha256Digest.Parse(new string('a', 64)),
            CreateArtifact("programDefinitionSnapshot", "definition.json"),
            new UnityProjectIdentity(project.UnityProject.UnityProjectRoot.Value, project.UnityProject.ProjectFingerprint, project.UnityProject.UnityVersion),
            CreateFixedContext(),
            host,
            Generation,
            Generation,
            execution.DeadlineUtc,
            StartedAtUtc,
            StartedAtUtc,
            ProgramRunState.Running,
            0,
            [step],
            [],
            ProgramCancellationRecord.None,
            null);
    }

    private static ProgramRunRecord CreateCallRun (ProjectContext project, ProgramStepExecutionReference execution)
    {
        var source = CreateRun(project, execution);
        var plan = CreateArtifact("requestPlan", "request-plan.json");
        var boundary = new ProgramRequestExecutionBoundary(
            execution.ExecutionId, source.Project, source.Host, Generation, Sha256Digest.Parse(new string('e', 64)),
            plan, null, [], [], execution.StartedAtUtc, execution.DeadlineUtc);
        var step = source.Steps[0] with
        {
            Command = "call",
            GenerationBefore = Generation,
            RequestPlanRef = plan,
            RequestExecution = boundary,
        };
        return new ProgramRunRecord(
            source.SchemaVersion, source.Version, source.RunId, source.DefinitionDigest, source.DefinitionSnapshotRef,
            source.Project, source.FixedContext, source.Host, source.StartedGeneration, source.CurrentEditorGeneration,
            source.DeadlineUtc, source.StartedAtUtc, source.UpdatedAtUtc, source.State, source.Cursor,
            [step], source.ChildExecutionRefs, source.Cancellation, source.TerminalRecordRef);
    }

    private static ProgramRunRecord ReplaceCommand (ProgramRunRecord source, string command) => new(
        source.SchemaVersion, source.Version, source.RunId, source.DefinitionDigest, source.DefinitionSnapshotRef,
        source.Project, source.FixedContext, source.Host, source.StartedGeneration, source.CurrentEditorGeneration,
        source.DeadlineUtc, source.StartedAtUtc, source.UpdatedAtUtc, source.State, source.Cursor,
            [source.Steps[0] with { Command = command }], source.ChildExecutionRefs, source.Cancellation, source.TerminalRecordRef);

    private static ProgramRunRecord ReplaceFixedContext (ProgramRunRecord source, ProgramRunFixedContext fixedContext) => new(
        source.SchemaVersion, source.Version, source.RunId, source.DefinitionDigest, source.DefinitionSnapshotRef,
        source.Project, fixedContext, source.Host, source.StartedGeneration, source.CurrentEditorGeneration,
        source.DeadlineUtc, source.StartedAtUtc, source.UpdatedAtUtc, source.State, source.Cursor,
        source.Steps, source.ChildExecutionRefs, source.Cancellation, source.TerminalRecordRef);

    private static async ValueTask<ProgramStepExecutionRecoveredTerminal?> InvokeCallTerminalAsync (
        ProgramRunRecord run,
        Guid executionId,
        IpcProgramRequestExecutionResponse response)
    {
        var port = (ProgramLifecycleStepExecutionPort)RuntimeHelpers.GetUninitializedObject(typeof(ProgramLifecycleStepExecutionPort));
        var method = typeof(ProgramLifecycleStepExecutionPort).GetMethod("ToCallTerminalAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var payload = JsonSerializer.SerializeToElement(response, IpcJsonSerializerOptions.Default);
        var task = (ValueTask<ProgramStepExecutionRecoveredTerminal?>)method.Invoke(
            port, [new UnityRequestResponse(payload, []), run, 0, executionId, CancellationToken.None])!;
        return await task;
    }

    private static LifecycleExecutionHostRegistration CreateHost () => new(
        new ProcessIdentity(32841, 639221708291771770),
        Guid.Parse("fcb43edd-76ed-4680-9392-f8463dc074b6"),
        Guid.Parse("574c28c1-5ef1-4171-9e00-3c6a7bc07512"),
        Guid.Parse("574c28c1-5ef1-4171-9e00-3c6a7bc07512"));

    private static LifecycleExecutionHostRegistration CreateSuccessorHost (
        LifecycleExecutionHostRegistration host) => new(
        host.Process,
        host.EditorInstanceId,
        Guid.Parse("774c28c1-5ef1-4171-9e00-3c6a7bc07512"),
        Guid.Parse("774c28c1-5ef1-4171-9e00-3c6a7bc07512"));

    private static LifecycleExecutionStartBinding CreateLifecycleStart (
        ResolvedUnityProjectContext project,
        LifecycleExecutionHostRegistration host,
        ActiveExecutionRef reference,
        UnityEditorGenerationSnapshot generation,
        ProgramStepExecutionReference execution) => new(
        reference,
        new UnityProjectIdentity(project.UnityProjectRoot.Value, project.ProjectFingerprint, project.UnityVersion),
        host,
        generation,
        execution.DeadlineUtc,
        execution.StartedAtUtc);

    private static ProgramRunFixedContext CreateFixedContext ()
    {
        var commandTimeouts = new Dictionary<string, int> { ["refresh"] = 1_000 };
        return new ProgramRunFixedContext(
            new ProgramEffectiveAuthorizationSnapshot(false, false, IpcProgramEffectiveAuthorizationSnapshot.ComputeDigest(false, false).ToString(), StartedAtUtc),
            new ProgramEffectiveConfigurationSnapshot(1, OperationPolicy.Safe, PlanTokenMode.Optional, ReadIndexMode.RequireFresh, [], 1_000, commandTimeouts, false,
                IpcProgramEffectiveConfigurationSnapshot.ComputeDigest(1, "safe", "optional", "requireFresh", [], 1_000, commandTimeouts, false), StartedAtUtc),
            new ProgramExecutionModeSnapshot("auto", "daemon"),
            new ProgramAttachedSupervisorSnapshot(Guid.Parse("e7599e03-77af-4b3c-b1a1-6e0718bf88b5"), Guid.Parse("cd403317-2b0c-4ae4-8ff7-8b50d2564554"), new ProcessIdentity(100, 10), ProgramSupervisorConnection.Connected, ProgramSupervisorAvailability.Available, StartedAtUtc));
    }

    private static ActiveExecutionRef CreateActiveReference (Guid executionId)
    {
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        return new ActiveExecutionRef(
            definition.ExecutionKind,
            executionId,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new ExecutionState(TextVocabulary.GetText(LifecycleExecutionState.Registered)),
            new ExecutionStatusLocator($".ucli/local/lifecycle-executions/refresh/{executionId:N}/execution.json"));
    }

    private static TerminalExecutionRef CreateTerminalReference (Guid executionId)
    {
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        return new TerminalExecutionRef(
            definition.ExecutionKind,
            executionId,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new ExecutionState(TextVocabulary.GetText(LifecycleExecutionState.Completed)),
            null,
            CreateArtifact("lifecycleExecutionTerminalRecord", $"lifecycle-execution/refresh/{executionId:N}/terminal.json"));
    }

    private static RefreshLifecycleExecutionTerminalRecord CreateCompletedRefreshTerminal (
        ResolvedUnityProjectContext project,
        LifecycleExecutionHostRegistration host,
        Guid executionId)
    {
        var observation = UnityEditorObservationTestFactory.Create(
            projectFingerprint: project.ProjectFingerprint,
            unityVersion: project.UnityVersion,
            generations: Generation,
            observedAtUtc: StartedAtUtc.AddSeconds(1));
        return new RefreshLifecycleExecutionTerminalRecord(
            executionId,
            LifecycleExecutionDefinitionDigest.Calculate(new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh)),
            new UnityProjectIdentity(project.UnityProjectRoot.Value, project.ProjectFingerprint, project.UnityVersion),
            host,
            Generation,
            Generation,
            StartedAtUtc.AddMinutes(3),
            StartedAtUtc,
            StartedAtUtc.AddSeconds(1),
            LifecycleExecutionTerminalReason.Completed,
            ExecutionApplicationState.Applied,
            new RefreshLifecycleResult(
                new RefreshLifecycleResult.RefreshEvidence(StartedAtUtc, StartedAtUtc.AddSeconds(1), 0, 0),
                observation,
                null),
            null,
            []);
    }

    private static ArtifactRef CreateArtifact (string kind, string path) => new PathArtifactRef(
        new ArtifactKind(kind), new ArtifactMediaType("application/json"), new ArtifactPath(path), Sha256Digest.Parse(new string('d', 64)), 1, StartedAtUtc);

    private sealed class ObservingRefreshService (
        ActiveExecutionRef activeReference,
        TerminalExecutionRef returnedReference,
        ResolvedUnityProjectContext project,
        LifecycleExecutionHostRegistration host) : IRefreshService
    {
        public int StartCount { get; private set; }

        public async ValueTask<RefreshExecutionResult> StartAsync (Guid requestId, LifecycleExecutionStartInvocation invocation, bool failFast, CancellationToken cancellationToken = default)
        {
            StartCount++;
            var observation = await invocation.StartObserver.ObserveAsync(new LifecycleExecutionStartBinding(
                activeReference,
                new UnityProjectIdentity(project.UnityProjectRoot.Value, project.ProjectFingerprint, project.UnityVersion),
                host,
                Generation,
                invocation.ExecutionDeadline.UtcDeadline,
                StartedAtUtc));
            Assert.IsType<LifecycleExecutionStartObservation.Observed>(observation);
            return RefreshExecutionResult.Failure(
                ApplicationFailure.InternalError("Refresh completed and returned its terminal record."),
                new RefreshExecutionErrorOutput(ProjectIdentityInfo.From(project), requestId, returnedReference, ExecutionApplicationState.Applied, null, null, null));
        }

        public ValueTask<RefreshExecutionResult> ReconnectAsync (Guid requestId, LifecycleExecutionReconnectInvocation invocation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class MemoryStore (ProgramRunRecord current) : IProgramRunStoreFactory, IProgramRunStore
    {
        public ProgramRunRecord Current { get; private set; } = current;
        public bool RejectNextCompareExchange { get; set; }
        public IProgramRunStore ForProject (ResolvedUnityProjectContext project) => this;
        public ValueTask<ArtifactRef> PublishDefinitionSnapshotAsync (Guid runId, ProgramDefinitionSnapshot snapshot, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ProgramRunStoreCreateResult> CreateAsync (ProgramRunRecord run, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ProgramRunRecord?> ReadAsync (Guid runId, CancellationToken cancellationToken = default) => ValueTask.FromResult<ProgramRunRecord?>(runId == Current.RunId ? Current : null);
        public ValueTask<ProgramRunStoredDefinition?> ReadDefinitionAsync (Guid runId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ProgramRunStoreCompareExchangeResult> CompareExchangeAsync (ProgramRunRecord expected, ProgramRunRecord replacement, CancellationToken cancellationToken = default)
        {
            if (RejectNextCompareExchange)
            {
                RejectNextCompareExchange = false;
                Current = CopyWithVersion(Current, Current.Version + 1);
                return ValueTask.FromResult(new ProgramRunStoreCompareExchangeResult(false, Current));
            }
            if (expected.Version != Current.Version)
            {
                return ValueTask.FromResult(new ProgramRunStoreCompareExchangeResult(false, Current));
            }
            Current = replacement;
            return ValueTask.FromResult(new ProgramRunStoreCompareExchangeResult(true, Current));
        }
        public ValueTask<ProgramRunTerminalPublicationResult> PublishRunTerminalAsync (ProgramRunRecord expected, ProgramRunTerminalRecord terminalRecord, Func<ArtifactRef, ProgramRunRecord> createReplacement, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ProgramRunTerminalPublicationResult> PublishRunTimeoutTerminalAsync (ProgramRunRecord expected, int stepIndex, ProgramRunTerminalRecord terminalRecord, Func<ArtifactRef, ProgramRunRecord> createReplacement, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ProgramRunStepTerminalPublicationResult> PublishStepTerminalAsync (ProgramRunRecord expected, int stepIndex, ProgramStepTerminalRecord terminalRecord, Func<ArtifactRef, ProgramRunRecord> createReplacement, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        private static ProgramRunRecord CopyWithVersion (ProgramRunRecord source, long version) => new(
            source.SchemaVersion,
            version,
            source.RunId,
            source.DefinitionDigest,
            source.DefinitionSnapshotRef,
            source.Project,
            source.FixedContext,
            source.Host,
            source.StartedGeneration,
            source.CurrentEditorGeneration,
            source.DeadlineUtc,
            source.StartedAtUtc,
            source.UpdatedAtUtc,
            source.State,
            source.Cursor,
            source.Steps,
            source.ChildExecutionRefs,
            source.Cancellation,
            source.TerminalRecordRef)
        {
            SupervisorObservation = source.SupervisorObservation,
            HostObservation = source.HostObservation,
            TerminalReasonCode = source.TerminalReasonCode,
        };
    }

    private sealed class ThrowingBinding (ResolvedUnityProjectContext project) : IUnityExecutionHostBinding
    {
        public ResolvedUnityProjectContext Project { get; } = project;
        public UnityExecutionTarget Target => UnityExecutionTarget.Daemon;
        public ValueTask<UnityRequestExecutionResult> ExecuteAsync (UcliCommand command, UnityRequestPayload payload, ExecutionDeadline deadline, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<UnityRequestExecutionResult> StartAsync (UcliCommand command, UnityRequestPayload payload, LifecycleExecutionStartInvocation invocation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<UnityRequestExecutionResult> ReconnectAsync (UcliCommand command, UnityRequestPayload payload, LifecycleExecutionReconnectInvocation invocation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask DisposeAsync () => ValueTask.CompletedTask;
    }

    private sealed class ThrowingReadyService : IReadyService
    {
        public ValueTask<ReadyExecutionResult> ExecuteAsync (ReadyCommandInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ProgramReadyObservation> ObserveOnFixedHostAsync (ProjectContext context, IUnityExecutionHostBinding binding, ExecutionDeadline deadline, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FixedReadyService (ProgramReadyObservation observation) : IReadyService
    {
        public ValueTask<ReadyExecutionResult> ExecuteAsync (ReadyCommandInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ProgramReadyObservation> ObserveOnFixedHostAsync (ProjectContext context, IUnityExecutionHostBinding binding, ExecutionDeadline deadline, CancellationToken cancellationToken = default) => ValueTask.FromResult(observation);
    }

    private sealed class ThrowingRefreshService : IRefreshService
    {
        public ValueTask<RefreshExecutionResult> StartAsync (Guid requestId, LifecycleExecutionStartInvocation invocation, bool failFast, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<RefreshExecutionResult> ReconnectAsync (Guid requestId, LifecycleExecutionReconnectInvocation invocation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingRefreshService : IRefreshService
    {
        public bool? FailFast { get; private set; }

        public ValueTask<RefreshExecutionResult> StartAsync (Guid requestId, LifecycleExecutionStartInvocation invocation, bool failFast, CancellationToken cancellationToken = default)
        {
            FailFast = failFast;
            return ValueTask.FromResult(RefreshExecutionResult.Failure(ApplicationFailure.InternalError("Stopped after recording fail-fast.")));
        }

        public ValueTask<RefreshExecutionResult> ReconnectAsync (Guid requestId, LifecycleExecutionReconnectInvocation invocation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingLifecycleExecutionReconnectResolver : ILifecycleExecutionReconnectResolver
    {
        public ValueTask<LifecycleExecutionReconnectResolution> ResolveAsync (ResolvedUnityProjectContext project, LifecycleExecutionDefinition definition, ExecutionRef reference, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class MemoryArtifactStoreFactory : IProgramArtifactStoreFactory, IProgramArtifactStore
    {
        public IProgramArtifactStore ForProject (ResolvedUnityProjectContext project) => this;
        public ValueTask<ArtifactRef> PublishAsync (Guid runId, ArtifactKind kind, ArtifactMediaType mediaType, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default) => ValueTask.FromResult(CreateArtifact(kind.Value, "step-result.json"));
        public ValueTask<byte[]?> ReadAsync (ArtifactRef artifact, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingScreenshotService : IScreenshotCaptureService
    {
        public ValueTask<ScreenshotCaptureResult> CaptureAsync (ScreenshotCaptureInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ScreenshotCaptureResult> CaptureOnFixedHostAsync (ProjectContext context, IUnityExecutionHostBinding binding, IpcScreenshotTarget target, PixelDimensions? requestedDimensions, ExecutionDeadline deadline, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingCompileService : ICompileService
    {
        public ValueTask<CompileExecutionResult> StartAsync (LifecycleExecutionStartInvocation invocation, ICommandProgressSink? progressSink = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<CompileExecutionResult> ReconnectAsync (LifecycleExecutionReconnectInvocation invocation, ICommandProgressSink? progressSink = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingCompileService : ICompileService
    {
        public LifecycleExecutionStartInvocation? Invocation { get; private set; }

        public ValueTask<CompileExecutionResult> StartAsync (LifecycleExecutionStartInvocation invocation, ICommandProgressSink? progressSink = null, CancellationToken cancellationToken = default)
        {
            Invocation = invocation;
            return ValueTask.FromResult<CompileExecutionResult>(CompileExecutionResult.Failed(ApplicationFailure.InternalError("Stopped after recording fail-fast."), null, null, ExecutionApplicationState.NotApplied));
        }

        public ValueTask<CompileExecutionResult> ReconnectAsync (LifecycleExecutionReconnectInvocation invocation, ICommandProgressSink? progressSink = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingPlayEnterService : IPlayEnterService
    {
        public ValueTask<PlayEnterExecutionResult> StartAsync (LifecycleExecutionStartInvocation invocation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PlayEnterExecutionResult> ReconnectAsync (LifecycleExecutionReconnectInvocation invocation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingPlayEnterService : IPlayEnterService
    {
        public LifecycleExecutionStartInvocation? Invocation { get; private set; }

        public ValueTask<PlayEnterExecutionResult> StartAsync (LifecycleExecutionStartInvocation invocation, CancellationToken cancellationToken = default)
        {
            Invocation = invocation;
            return ValueTask.FromResult(PlayEnterExecutionResult.Failure(ApplicationFailure.InternalError("Stopped after recording fail-fast.")));
        }

        public ValueTask<PlayEnterExecutionResult> ReconnectAsync (LifecycleExecutionReconnectInvocation invocation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingPlayExitService : IPlayExitService
    {
        public ValueTask<PlayExitExecutionResult> StartAsync (LifecycleExecutionStartInvocation invocation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PlayExitExecutionResult> ReconnectAsync (LifecycleExecutionReconnectInvocation invocation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingPlayExitService : IPlayExitService
    {
        public LifecycleExecutionStartInvocation? Invocation { get; private set; }

        public ValueTask<PlayExitExecutionResult> StartAsync (LifecycleExecutionStartInvocation invocation, CancellationToken cancellationToken = default)
        {
            Invocation = invocation;
            return ValueTask.FromResult(PlayExitExecutionResult.Failure(ApplicationFailure.InternalError("Stopped after recording fail-fast.")));
        }

        public ValueTask<PlayExitExecutionResult> ReconnectAsync (LifecycleExecutionReconnectInvocation invocation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingArtifactStoreFactory : IProgramArtifactStoreFactory
    {
        public IProgramArtifactStore ForProject (ResolvedUnityProjectContext project) => throw new NotSupportedException();
    }
}
