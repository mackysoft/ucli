using MackySoft.Ucli.Application.Features.Assurance.Compile.Execution;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Compile;

internal static class CompileServiceTestSupport
{
    public static readonly Guid ExecutionId =
        Guid.Parse("0b143533-fbc2-41ee-bc33-08d80b4fc359");
    public static readonly Guid OtherExecutionId =
        Guid.Parse("5d948e1f-d4cd-4357-9f79-eb86604cd355");
    public static readonly DateTimeOffset StartedAtUtc =
        DateTimeOffset.Parse("2026-05-17T00:00:00Z");

    public static CompileService CreateService (
        IProjectContextResolver? projectContextResolver = null,
        IUnityExecutionModeDecisionService? modeDecisionService = null,
        IUnityRequestExecutor? unityRequestExecutor = null,
        ILifecycleExecutionReconnectResolver? reconnectResolver = null,
        IGuidGenerator? executionIdGenerator = null,
        TimeProvider? timeProvider = null,
        ILifecycleExecutionHostExitTerminalizer? hostExitTerminalizer = null)
    {
        var resolvedTimeProvider =
            timeProvider ?? new FakeTimeProvider(StartedAtUtc);
        return new CompileService(
            reconnectResolver
                ?? new RecordingLifecycleExecutionReconnectResolver(
                    CreateTerminalResolution(
                        CreateResult(),
                        Verdict.Pass)),
            hostExitTerminalizer
                ?? new UnexpectedLifecycleExecutionHostExitTerminalizer(),
            new LifecycleExecutionRegistrationIssuer(
                executionIdGenerator
                    ?? new StaticGuidGenerator(ExecutionId),
                resolvedTimeProvider),
            resolvedTimeProvider);
    }

    public static async ValueTask<LifecycleExecutionStartInvocation>
        CreateStartInvocationAsync (
            RecordingUnityRequestExecutor requestExecutor,
            ProjectContext? context = null,
            UnityExecutionMode mode = UnityExecutionMode.Oneshot,
            TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(requestExecutor);
        var resolvedContext = context ?? ProjectContextTestFactory.CreateSingleRootProject();
        var deadline = ExecutionDeadline.Start(
            TimeSpan.FromSeconds(10),
            timeProvider ?? new FakeTimeProvider(StartedAtUtc));
        var bindingResult = await requestExecutor.BindAsync(
                mode,
                resolvedContext.UnityProject,
                deadline)
            .ConfigureAwait(false);
        return new LifecycleExecutionStartInvocation(
            new LifecycleExecutionFixedContext(
                resolvedContext,
                mode,
                bindingResult.Binding!),
            deadline,
            deadline.CreateCompletionDeadline(
                LifecycleExecutionTiming.ResponseDeliveryGrace),
            NullLifecycleExecutionStartObserver.Instance);
    }

    public static async ValueTask<LifecycleExecutionReconnectInvocation>
        CreateReconnectInvocationAsync (
            RecordingUnityRequestExecutor requestExecutor,
            ExecutionRef executionReference,
            ProjectContext? context = null,
            UnityExecutionMode mode = UnityExecutionMode.Oneshot,
            TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(requestExecutor);
        ArgumentNullException.ThrowIfNull(executionReference);
        var resolvedContext = context ?? ProjectContextTestFactory.CreateSingleRootProject();
        var deadline = ExecutionDeadline.Start(
            TimeSpan.FromSeconds(13),
            timeProvider ?? new FakeTimeProvider(StartedAtUtc));
        var bindingResult = await requestExecutor.BindAsync(
                mode,
                resolvedContext.UnityProject,
                deadline)
            .ConfigureAwait(false);
        return new LifecycleExecutionReconnectInvocation(
            new LifecycleExecutionFixedContext(
                resolvedContext,
                mode,
                bindingResult.Binding!),
            executionReference,
            deadline);
    }

    public static UnityRequestExecutionResult CreateCompileResponseResult (
        CompileLifecycleResult result,
        Guid? executionId = null)
    {
        var actualExecutionId = executionId ?? ExecutionId;
        return UnityRequestExecutionResult.Success(
            new UnityRequestResponse(
                IpcPayloadCodec.SerializeToElement(
                    new IpcCompileResponse(
                        CreateTerminalReference(actualExecutionId),
                        result)),
                []),
            CreateStart(actualExecutionId));
    }

    public static UnityRequestExecutionResult CreateCompileErrorResponseResult (
        ExecutionApplicationState applicationState,
        OperationExecutionError error,
        ExecutionRef? lifecycleExecutionRef = null,
        CompileLifecycleResult? result = null,
        UnityEditorObservation? observedLifecycle = null)
    {
        return UnityRequestExecutionResult.Success(
            new UnityRequestResponse(
                IpcPayloadCodec.SerializeToElement(
                    new IpcCompileErrorResponse(
                        lifecycleExecutionRef,
                        applicationState,
                        result,
                        observedLifecycle)),
                [error]),
            lifecycleExecutionRef == null
                ? null
                : CreateStart(lifecycleExecutionRef.Id));
    }

    public static LifecycleExecutionReconnectResolution.Terminal
        CreateTerminalResolution (
            CompileLifecycleResult result,
            Verdict verdict,
            Guid? executionId = null)
    {
        var terminalReference = CreateTerminalReference(executionId);
        return new LifecycleExecutionReconnectResolution.Terminal(
            terminalReference,
            CreateCompletedTerminalRecord(
                result,
                verdict,
                executionId));
    }

    public static CompileLifecycleExecutionTerminalRecord
        CreateCompletedTerminalRecord (
            CompileLifecycleResult result,
            Verdict verdict,
            Guid? executionId = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        var actualExecutionId = executionId ?? ExecutionId;
        var start = CreateStart(actualExecutionId);
        return new CompileLifecycleExecutionTerminalRecord(
            actualExecutionId,
            start.LifecycleExecutionRef.DefinitionDigest,
            start.Project,
            start.Host,
            start.StartedGeneration,
            result.Lifecycle.State?.Generations
                ?? throw new ArgumentException(
                    "A completed compile test result requires terminal generation evidence.",
                    nameof(result)),
            start.DeadlineUtc,
            start.StartedAtUtc,
            StartedAtUtc.AddSeconds(5),
            LifecycleExecutionTerminalReason.Completed,
            ExecutionApplicationState.Applied,
            result,
            verdict,
            Array.Empty<ArtifactRef>());
    }

    public static RecoveryExecutionRef CreatePublishingReference (
        Guid? executionId = null)
    {
        var actualExecutionId = executionId ?? ExecutionId;
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Compile);
        return new RecoveryExecutionRef(
            definition.ExecutionKind,
            actualExecutionId,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new ExecutionState(TextVocabulary.GetText(
                LifecycleExecutionState.Publishing)),
            new ExecutionStatusLocator(
                $"lifecycle-executions/{actualExecutionId:N}/status.json"));
    }

    public static LifecycleExecutionStartBinding CreateStart (
        Guid? executionId = null)
    {
        var actualExecutionId = executionId ?? ExecutionId;
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Compile);
        var registrationGeneration =
            Guid.Parse("10000000-0000-0000-0000-000000000001");
        return new LifecycleExecutionStartBinding(
            new ActiveExecutionRef(
                definition.ExecutionKind,
                actualExecutionId,
                LifecycleExecutionDefinitionDigest.Calculate(definition),
                new ExecutionState("registered"),
                new ExecutionStatusLocator(
                    $"lifecycle-executions/{actualExecutionId:N}/status.json")),
            new UnityProjectIdentity(
                ProjectContextTestFactory.UnityProjectRoot,
                ProjectContextTestFactory.ProjectFingerprint,
                ProjectContextTestFactory.UnityVersion),
            new LifecycleExecutionHostRegistration(
                new ProcessIdentity(4200, 123456),
                Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                registrationGeneration,
                registrationGeneration),
            new UnityEditorGenerationSnapshot(
                CompileGeneration: 12,
                DomainReloadGeneration: 7,
                AssetRefreshGeneration: 3,
                PlayModeGeneration: 2),
            StartedAtUtc.AddSeconds(10),
            StartedAtUtc);
    }

    public static TerminalExecutionRef CreateTerminalReference (
        Guid? executionId = null)
    {
        return CreateTerminalReference(
            LifecycleExecutionState.Completed,
            executionId);
    }

    public static TerminalExecutionRef CreateFailedTerminalReference (
        Guid? executionId = null)
    {
        return CreateTerminalReference(
            LifecycleExecutionState.Failed,
            executionId);
    }

    private static TerminalExecutionRef CreateTerminalReference (
        LifecycleExecutionState state,
        Guid? executionId)
    {
        var actualExecutionId = executionId ?? ExecutionId;
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Compile);
        return new TerminalExecutionRef(
            definition.ExecutionKind,
            actualExecutionId,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new ExecutionState(TextVocabulary.GetText(state)),
            new ExecutionStatusLocator(
                $"lifecycle-executions/{actualExecutionId:N}/status.json"),
            new PathArtifactRef(
                LifecycleExecutionArtifactContract.TerminalRecordKind,
                LifecycleExecutionArtifactContract.TerminalRecordMediaType,
                new ArtifactPath(
                    $"lifecycle-executions/{actualExecutionId:N}/terminal-record.json"),
                Sha256Digest.Parse(new string('f', 64)),
                sizeBytes: 512,
                StartedAtUtc.AddSeconds(5)));
    }

    public static CompileLifecycleResult CreateResult (int errorCount = 0)
    {
        var primaryDiagnostic = errorCount == 0
            ? null
            : new UnityEditorPrimaryDiagnostic(
                Kind: UnityEditorPrimaryDiagnosticKind.Compiler,
                Code: "CS1002",
                File: "Assets/Broken.cs",
                Line: 4,
                Column: 16,
                Message: "; expected");
        var canAcceptExecutionRequests = errorCount == 0;
        return new CompileLifecycleResult(
            new CompileLifecycleResult.RefreshEvidence(
                Origin: CompileLifecycleRefreshOrigin.AssetDatabaseRefresh,
                Requested: true,
                StartedAtUtc: StartedAtUtc,
                CompletedAtUtc: StartedAtUtc.AddSeconds(2),
                Completed: true),
            new CompileLifecycleResult.ScriptCompilationEvidence(
                Started: true,
                Completed: true,
                CompileGenerationBefore: 12,
                CompileGenerationAfter: 14,
                Diagnostics: new CompileLifecycleResult.DiagnosticsEvidence(
                    ErrorCount: errorCount,
                    WarningCount: 0,
                    PrimaryDiagnostic: primaryDiagnostic)),
            new CompileLifecycleResult.DomainReloadEvidence(
                ReloadRequired: false,
                ReloadObserved: false,
                GenerationBefore: 7,
                GenerationAfter: 7,
                Settled: true),
            new CompileLifecycleResult.LifecycleEvidence(
                ServerVersion: "0.5.0",
                UnityVersion: ProjectContextTestFactory.UnityVersion,
                State: new UnityEditorStateSnapshot(
                    editorMode: UnityEditorMode.Batchmode,
                    lifecycleState: canAcceptExecutionRequests
                        ? UnityEditorLifecycleState.Ready
                        : UnityEditorLifecycleState.CompileFailed,
                    compileState: canAcceptExecutionRequests
                        ? UnityEditorCompileState.Ready
                        : UnityEditorCompileState.Failed,
                    generations: new UnityEditorGenerationSnapshot(
                        CompileGeneration: 14,
                        DomainReloadGeneration: 7,
                        AssetRefreshGeneration: 3,
                        PlayModeGeneration: 2),
                    playMode: new UnityEditorPlayModeSnapshot(
                        State: UnityEditorPlayModeState.Stopped,
                        Transition: UnityEditorPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false)),
                ObservedAtUtc: StartedAtUtc.AddSeconds(3),
                ActionRequired: canAcceptExecutionRequests
                    ? null
                    : UnityEditorActionRequired.FixCompileErrors,
                PrimaryDiagnostic: primaryDiagnostic));
    }
}
