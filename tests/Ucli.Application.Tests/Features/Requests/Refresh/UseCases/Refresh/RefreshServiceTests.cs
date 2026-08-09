using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Application.Tests.Refresh;

public sealed class RefreshServiceTests
{
    private static readonly Guid RequestId =
        Guid.Parse("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62");
    private static readonly Guid ExecutionId =
        Guid.Parse("ab0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c63");
    private static readonly Guid OtherExecutionId =
        Guid.Parse("bb0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c64");
    private static readonly DateTimeOffset StartedAtUtc =
        new(2026, 7, 31, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenHostExitIsConfirmedAfterDispatch_PublishesIndeterminateUnityExitedTerminal ()
    {
        var context = ProjectContextTestFactory.CreateRepositoryFixtureProject();
        var registration = new LifecycleExecutionRegistration(
            new LifecycleExecutionDefinition(
                LifecycleExecutionKind.Refresh),
            ExecutionId,
            StartedAtUtc.AddMinutes(42),
            StartedAtUtc.AddMinutes(-3));
        var start = CreateStartBinding(context, registration);
        var terminalReference = CreateTerminalReference(
            registration,
            completed: false);
        var terminalRecord = new RefreshLifecycleExecutionTerminalRecord(
            ExecutionId,
            start.LifecycleExecutionRef.DefinitionDigest,
            start.Project,
            start.Host,
            start.StartedGeneration,
            terminalGeneration: null,
            start.DeadlineUtc,
            start.StartedAtUtc,
            StartedAtUtc,
            LifecycleExecutionTerminalReason.UnityExited,
            ExecutionApplicationState.Indeterminate,
            result: null,
            verdict: null,
            Array.Empty<ArtifactRef>());
        var terminalizer = new RecordingLifecycleExecutionHostExitTerminalizer(
            new LifecycleExecutionHostExitTerminalizationResult.Published(
                terminalReference,
                terminalRecord));
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Failure(
                new UnityRequestFailure(
                    UnityRequestFailureKind.General,
                    EditorLifecycleErrorCodes.EditorUnavailable,
                    "The fixed Unity host exited."),
                start,
                lifecycleActionDispatched: true,
                new LifecycleExecutionHostExitObservation(
                    start.Host.Process)));
        var postconditionStore =
            new TestMutationReadPostconditionStore();
        var service = CreateService(
            context,
            requestExecutor,
            postconditionStore,
            hostExitTerminalizer: terminalizer,
            timeProvider: new FakeTimeProvider(StartedAtUtc));

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            LifecycleExecutionErrorCodes.UnityExited,
            Assert.Single(result.Failures).Code);
        Assert.Same(
            terminalReference,
            result.ErrorOutput!.LifecycleExecutionRef);
        Assert.Equal(
            ExecutionApplicationState.Indeterminate,
            result.ErrorOutput.ApplicationState);
        Assert.Single(postconditionStore.WriteInvocations);
        var invocation = Assert.Single(terminalizer.Invocations);
        Assert.IsType<RefreshLifecycleExecutionTerminalRecord>(
            invocation.TerminalRecord);
        Assert.Equal(
            LifecycleExecutionTerminalReason.UnityExited,
            invocation.TerminalReason);
        Assert.Equal(
            ExecutionApplicationState.Indeterminate,
            invocation.ApplicationState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenHostExitTerminalPublicationFails_PreservesFixedRefreshEvidence ()
    {
        var context =
            ProjectContextTestFactory.CreateRepositoryFixtureProject();
        var registration = new LifecycleExecutionRegistration(
            new LifecycleExecutionDefinition(
                LifecycleExecutionKind.Refresh),
            ExecutionId,
            StartedAtUtc.AddMinutes(42),
            StartedAtUtc.AddMinutes(-3));
        var start = CreateStartBinding(context, registration);
        var readPostcondition = CreateReadPostcondition(StartedAtUtc);
        var typedResult = new RefreshLifecycleResult(
            new RefreshLifecycleResult.RefreshEvidence(
                StartedAtUtc,
                StartedAtUtc.AddSeconds(2),
                domainReloadGenerationBefore: 1,
                domainReloadGenerationAfter: 2),
            CreateObservation(
                context.UnityProject.ProjectFingerprint,
                domainReloadGeneration: 2),
            readPostcondition);
        var fixedTerminalRecord = new RefreshLifecycleExecutionTerminalRecord(
            ExecutionId,
            start.LifecycleExecutionRef.DefinitionDigest,
            start.Project,
            start.Host,
            start.StartedGeneration,
            typedResult.Lifecycle.State.Generations,
            start.DeadlineUtc,
            start.StartedAtUtc,
            StartedAtUtc.AddSeconds(2),
            LifecycleExecutionTerminalReason.Completed,
            ExecutionApplicationState.Applied,
            typedResult,
            verdict: null,
            Array.Empty<ArtifactRef>());
        var publishingReference = CreatePublishingReference(registration);
        var terminalizer = new RecordingLifecycleExecutionHostExitTerminalizer(
            new LifecycleExecutionHostExitTerminalizationResult.PublicationFailed(
                publishingReference,
                ExecutionApplicationState.Applied,
                ApplicationFailure.InternalError(
                    "Terminal Record publication failed.",
                    LifecycleExecutionErrorCodes.TerminalPublicationFailed),
                fixedTerminalRecord));
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Failure(
                new UnityRequestFailure(
                    UnityRequestFailureKind.General,
                    EditorLifecycleErrorCodes.EditorUnavailable,
                    "The fixed Unity host exited."),
                start,
                lifecycleActionDispatched: true,
                new LifecycleExecutionHostExitObservation(
                    start.Host.Process)));
        var postconditionStore = new TestMutationReadPostconditionStore();
        var service = CreateService(
            context,
            requestExecutor,
            postconditionStore,
            hostExitTerminalizer: terminalizer,
            timeProvider: new FakeTimeProvider(StartedAtUtc));

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            LifecycleExecutionErrorCodes.TerminalPublicationFailed,
            Assert.Single(result.Failures).Code);
        Assert.Same(
            publishingReference,
            result.ErrorOutput!.LifecycleExecutionRef);
        Assert.Equal(
            typedResult.Refresh.StartedAtUtc,
            result.ErrorOutput.Refresh!.StartedAtUtc);
        Assert.Equal(typedResult.Lifecycle, result.ErrorOutput.ObservedLifecycle);
        Assert.Equal(
            readPostcondition.Requirements,
            result.ErrorOutput.ReadPostcondition!.Requirements);
        Assert.Equal(
            readPostcondition.Requirements,
            Assert.Single(postconditionStore.WriteInvocations)
                .ReadPostcondition.Requirements);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenLifecycleStartRejectsProject_PreservesTypedStartErrorWithoutActionPayload ()
    {
        var context = ProjectContextTestFactory.CreateRepositoryFixtureProject();
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                new UnityRequestResponse(
                    IpcPayloadCodec.SerializeToElement(new { }),
                    [
                        new OperationExecutionError(
                            LifecycleExecutionErrorCodes.ProjectMismatch,
                            "Lifecycle Execution belongs to another project.",
                            "/project"),
                    ])));
        var postconditionStore = new TestMutationReadPostconditionStore();
        var service = CreateService(
            context,
            requestExecutor,
            postconditionStore);

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(
            LifecycleExecutionErrorCodes.ProjectMismatch,
            failure.Code);
        Assert.Equal("/project", failure.InstancePath);
        Assert.Null(result.ErrorOutput!.LifecycleExecutionRef);
        Assert.Equal(
            ExecutionApplicationState.NotApplied,
            result.ErrorOutput.ApplicationState);
        Assert.Empty(postconditionStore.WriteInvocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenCallerCancelsAsResponseArrives_PersistsReadPostconditionAndReturnsTerminalResult ()
    {
        using var callerCancellation = new CancellationTokenSource();
        var context = ProjectContextTestFactory.CreateRepositoryFixtureProject();
        var readPostcondition = CreateReadPostcondition(
            StartedAtUtc.AddSeconds(1));
        var requestExecutor = new RecordingUnityRequestExecutor(payload =>
        {
            var refresh = Assert.IsType<UnityRequestPayload.Refresh>(payload);
            var result = new RefreshLifecycleResult(
                new RefreshLifecycleResult.RefreshEvidence(
                    StartedAtUtc,
                    StartedAtUtc.AddSeconds(2),
                    domainReloadGenerationBefore: 1,
                    domainReloadGenerationAfter: 2),
                CreateObservation(
                    context.UnityProject.ProjectFingerprint,
                    domainReloadGeneration: 2),
                readPostcondition);
            var response = new IpcRefreshResponse(
                CreateIpcProject(context),
                CreateTerminalReference(
                    refresh.Registration,
                    completed: true),
                result);
            return UnityRequestExecutionResult.Success(
                new UnityRequestResponse(
                    IpcPayloadCodec.SerializeToElement(response),
                    []),
                CreateStartBinding(
                    context,
                    refresh.Registration));
        })
        {
            OnExecute = _ => callerCancellation.Cancel(),
        };
        var postconditionStore = new TestMutationReadPostconditionStore();
        var service = CreateService(
            context,
            requestExecutor,
            postconditionStore);

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(),
            callerCancellation.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ExecutionLifecycle.Terminal,
            result.Output!.LifecycleExecutionRef.Lifecycle);
        var write = Assert.Single(
            postconditionStore.WriteInvocations);
        Assert.False(write.CancellationToken.CanBeCanceled);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_RegistersBeforeDispatchAndReturnsTypedTerminalResult ()
    {
        var context = ProjectContextTestFactory.CreateRepositoryFixtureProject();
        var readPostcondition = CreateReadPostcondition(StartedAtUtc.AddSeconds(1));
        var requestExecutor = new RecordingUnityRequestExecutor(payload =>
        {
            var refresh = Assert.IsType<UnityRequestPayload.Refresh>(payload);
            var observation = CreateObservation(
                context.UnityProject.ProjectFingerprint,
                domainReloadGeneration: 2);
            var result = new RefreshLifecycleResult(
                new RefreshLifecycleResult.RefreshEvidence(
                    StartedAtUtc,
                    StartedAtUtc.AddSeconds(2),
                    domainReloadGenerationBefore: 1,
                    domainReloadGenerationAfter: 2),
                observation,
                readPostcondition);
            var response = new IpcRefreshResponse(
                CreateIpcProject(context),
                CreateTerminalReference(refresh.Registration, completed: true),
                result);
            return UnityRequestExecutionResult.Success(new UnityRequestResponse(
                IpcPayloadCodec.SerializeToElement(response),
                []));
        });
        var postconditionStore = new TestMutationReadPostconditionStore();
        var service = CreateService(context, requestExecutor, postconditionStore);

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RequestId, result.Output!.RequestId);
        Assert.Equal(ExecutionId, result.Output.LifecycleExecutionRef.Id);
        Assert.Equal(ExecutionLifecycle.Terminal, result.Output.LifecycleExecutionRef.Lifecycle);
        Assert.NotNull(result.Output.ReadPostcondition);
        Assert.Equal(
            readPostcondition.Requirements,
            result.Output.ReadPostcondition.Requirements);
        var invocation = Assert.Single(requestExecutor.Invocations);
        Assert.Equal(UcliCommandIds.Refresh, invocation.Command);
        Assert.Equal(UnityExecutionMode.Oneshot, invocation.Mode);
        Assert.Equal(TimeSpan.FromMilliseconds(4234), invocation.Timeout);
        var request = Assert.IsType<UnityRequestPayload.Refresh>(invocation.Payload);
        Assert.Equal(LifecycleExecutionKind.Refresh, request.Registration.Definition.Kind);
        Assert.Equal(ExecutionId, request.Registration.ExecutionId);
        Assert.Equal(StartedAtUtc, request.Registration.StartedAtUtc);
        Assert.Equal(StartedAtUtc.AddMilliseconds(1234), request.Registration.DeadlineUtc);
        Assert.True(
            Assert.IsType<RefreshLifecycleExecutionStartAdmissionPolicy>(
                request.StartAdmissionPolicy)
                .FailFast);
        Assert.Single(postconditionStore.WriteInvocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenReadPostconditionPersistenceFails_PreservesCompletedTypedResult ()
    {
        var context = ProjectContextTestFactory.CreateRepositoryFixtureProject();
        var readPostcondition = CreateReadPostcondition(StartedAtUtc.AddSeconds(1));
        var requestExecutor = new RecordingUnityRequestExecutor(payload =>
        {
            var refresh = Assert.IsType<UnityRequestPayload.Refresh>(payload);
            var typedResult = new RefreshLifecycleResult(
                new RefreshLifecycleResult.RefreshEvidence(
                    StartedAtUtc,
                    StartedAtUtc.AddSeconds(2),
                    domainReloadGenerationBefore: 1,
                    domainReloadGenerationAfter: 2),
                CreateObservation(
                    context.UnityProject.ProjectFingerprint,
                    domainReloadGeneration: 2),
                readPostcondition);
            var response = new IpcRefreshResponse(
                CreateIpcProject(context),
                CreateTerminalReference(refresh.Registration, completed: true),
                typedResult);
            return UnityRequestExecutionResult.Success(new UnityRequestResponse(
                IpcPayloadCodec.SerializeToElement(response),
                []));
        });
        var postconditionStore = new TestMutationReadPostconditionStore
        {
            WriteResult = MutationReadPostconditionStoreOperationResult.Failure(
                ExecutionError.InternalError(
                    "Read postcondition persistence failed.",
                    UcliCoreErrorCodes.InternalError)),
        };
        var service = CreateService(context, requestExecutor, postconditionStore);

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorOutput!.Refresh);
        Assert.NotNull(result.ErrorOutput.ObservedLifecycle);
        Assert.NotNull(result.ErrorOutput.ReadPostcondition);
        Assert.Equal(
            ExecutionLifecycle.Terminal,
            result.ErrorOutput.LifecycleExecutionRef!.Lifecycle);
        Assert.Equal(
            TextVocabulary.GetText(LifecycleExecutionState.Completed),
            result.ErrorOutput.LifecycleExecutionRef.State.Value);
        Assert.Single(postconditionStore.WriteInvocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenActionReturnsTypedFailure_PreservesFactsAndInvalidatesReadSurfaces ()
    {
        using var callerCancellation = new CancellationTokenSource();
        var context = ProjectContextTestFactory.CreateRepositoryFixtureProject();
        var requestExecutor = new RecordingUnityRequestExecutor(payload =>
        {
            var refresh = Assert.IsType<UnityRequestPayload.Refresh>(payload);
            var reference = CreateTerminalReference(refresh.Registration, completed: false);
            var errorPayload = new IpcRefreshErrorResponse(
                CreateIpcProject(context),
                reference,
                ExecutionApplicationState.Applied,
                new RefreshLifecycleStartEvidence(StartedAtUtc, 1),
                CreateObservation(
                    context.UnityProject.ProjectFingerprint,
                    domainReloadGeneration: 1),
                readPostcondition: null);
            return UnityRequestExecutionResult.Success(
                new UnityRequestResponse(
                    IpcPayloadCodec.SerializeToElement(errorPayload),
                    [
                        new OperationExecutionError(
                            LifecycleExecutionErrorCodes.DeadlineExceeded,
                            "Refresh deadline exceeded.",
                            null),
                    ]),
                CreateStartBinding(
                    context,
                    refresh.Registration));
        })
        {
            OnExecute = _ => callerCancellation.Cancel(),
        };
        var postconditionStore = new TestMutationReadPostconditionStore();
        var service = CreateService(context, requestExecutor, postconditionStore);

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(),
            callerCancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionId, result.ErrorOutput!.LifecycleExecutionRef!.Id);
        Assert.Equal(ExecutionApplicationState.Applied, result.ErrorOutput.ApplicationState);
        Assert.NotNull(result.ErrorOutput.Refresh);
        var write = Assert.Single(postconditionStore.WriteInvocations);
        Assert.False(write.CancellationToken.CanBeCanceled);
        var persisted = write.ReadPostcondition;
        Assert.Equal(3, persisted.Requirements.Count);
        Assert.Contains(
            persisted.Requirements,
            static requirement =>
                requirement.Surface == ExecutionReadPostconditionSurface.SceneTreeLite
                && requirement.ScenePath is null);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenTerminalPublicationFails_PreservesEvidenceAndRecoveryReference ()
    {
        var context = ProjectContextTestFactory.CreateRepositoryFixtureProject();
        var readPostcondition = CreateReadPostcondition(StartedAtUtc.AddSeconds(1));
        var typedResult = new RefreshLifecycleResult(
            new RefreshLifecycleResult.RefreshEvidence(
                StartedAtUtc,
                StartedAtUtc.AddSeconds(2),
                domainReloadGenerationBefore: 1,
                domainReloadGenerationAfter: 2),
            CreateObservation(
                context.UnityProject.ProjectFingerprint,
                domainReloadGeneration: 2),
            readPostcondition);
        var requestExecutor = new RecordingUnityRequestExecutor(payload =>
        {
            var refresh = Assert.IsType<UnityRequestPayload.Refresh>(payload);
            var errorPayload = new IpcRefreshErrorResponse(
                CreateIpcProject(context),
                CreatePublishingReference(refresh.Registration),
                ExecutionApplicationState.Applied,
                new RefreshLifecycleStartEvidence(StartedAtUtc, 1),
                typedResult.Lifecycle,
                typedResult.ReadPostcondition);
            return UnityRequestExecutionResult.Success(
                new UnityRequestResponse(
                    IpcPayloadCodec.SerializeToElement(errorPayload),
                    [
                        new OperationExecutionError(
                            LifecycleExecutionErrorCodes.TerminalPublicationFailed,
                            "Refresh terminal record could not be published.",
                            null),
                    ]),
                CreateStartBinding(
                    context,
                    refresh.Registration));
        });
        var postconditionStore = new TestMutationReadPostconditionStore();
        var service = CreateService(context, requestExecutor, postconditionStore);

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            LifecycleExecutionErrorCodes.TerminalPublicationFailed,
            Assert.Single(result.Failures).Code);
        Assert.Equal(
            ExecutionLifecycle.Recovery,
            result.ErrorOutput!.LifecycleExecutionRef!.Lifecycle);
        Assert.Equal(
            TextVocabulary.GetText(LifecycleExecutionState.Publishing),
            result.ErrorOutput.LifecycleExecutionRef.State.Value);
        Assert.Equal(
            typedResult.Refresh.StartedAtUtc,
            result.ErrorOutput.Refresh!.StartedAtUtc);
        Assert.Equal(
            typedResult.Lifecycle,
            result.ErrorOutput.ObservedLifecycle);
        var expectedPostcondition = Assert.IsType<ExecutionReadPostcondition>(
            typedResult.ReadPostcondition);
        var retainedPostcondition = Assert.IsType<ExecutionReadPostcondition>(
            result.ErrorOutput.ReadPostcondition);
        Assert.Equal(
            expectedPostcondition.Requirements,
            retainedPostcondition.Requirements);
        Assert.Equal(
            typedResult.Refresh.StartedAtUtc,
            result.ErrorOutput.Refresh!.StartedAtUtc);
        Assert.Equal(
            typedResult.Refresh.DomainReloadGenerationBefore,
            result.ErrorOutput.Refresh.DomainReloadGenerationBefore);
        Assert.Equal(typedResult.Lifecycle, result.ErrorOutput.ObservedLifecycle);
        Assert.Single(postconditionStore.WriteInvocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenCallerCancelsAfterRegistration_ReturnsReconnectableReference ()
    {
        using var callerCancellation = new CancellationTokenSource();
        var context = ProjectContextTestFactory.CreateRepositoryFixtureProject();
        var requestExecutor = new RecordingUnityRequestExecutor(payload =>
        {
            var refresh = Assert.IsType<UnityRequestPayload.Refresh>(payload);
            return UnityRequestExecutionResult.Failure(
                new UnityRequestFailure(
                        UnityRequestFailureKind.General,
                        ExecutionErrorCodes.Canceled,
                        "Waiting for refresh was canceled."),
                    CreateStartBinding(context, refresh.Registration),
                    lifecycleActionDispatched: true);
        })
        {
            OnExecute = _ => callerCancellation.Cancel(),
        };
        var postconditionStore = new TestMutationReadPostconditionStore();
        var service = CreateService(context, requestExecutor, postconditionStore);

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(),
            callerCancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.Canceled, Assert.Single(result.Failures).Code);
        Assert.Equal(ExecutionId, result.ErrorOutput!.LifecycleExecutionRef!.Id);
        Assert.Equal(ExecutionApplicationState.Indeterminate, result.ErrorOutput.ApplicationState);
        Assert.Single(postconditionStore.WriteInvocations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenResponseIdentifiesAnotherExecution_RetainsTrustedStart (
        bool isErrorResponse)
    {
        var context = ProjectContextTestFactory.CreateRepositoryFixtureProject();
        var requestExecutor = new RecordingUnityRequestExecutor(payload =>
        {
            var refresh = Assert.IsType<UnityRequestPayload.Refresh>(payload);
            var start = CreateStartBinding(context, refresh.Registration);
            var reference = CreateTerminalReference(
                refresh.Registration,
                completed: !isErrorResponse,
                OtherExecutionId);
            if (isErrorResponse)
            {
                var errorPayload = new IpcRefreshErrorResponse(
                    CreateIpcProject(context),
                    reference,
                    ExecutionApplicationState.Indeterminate,
                    refresh: null,
                    observedLifecycle: null,
                    readPostcondition: null);
                return UnityRequestExecutionResult.Success(
                    new UnityRequestResponse(
                        IpcPayloadCodec.SerializeToElement(errorPayload),
                        [
                            new OperationExecutionError(
                                LifecycleExecutionErrorCodes.DeadlineExceeded,
                                "Refresh deadline exceeded.",
                                null),
                        ]),
                    start);
            }

            var response = new IpcRefreshResponse(
                CreateIpcProject(context),
                reference,
                new RefreshLifecycleResult(
                    new RefreshLifecycleResult.RefreshEvidence(
                        StartedAtUtc,
                        StartedAtUtc.AddSeconds(2),
                        domainReloadGenerationBefore: 1,
                        domainReloadGenerationAfter: 2),
                    CreateObservation(
                        context.UnityProject.ProjectFingerprint,
                        domainReloadGeneration: 2),
                    readPostcondition: null));
            return UnityRequestExecutionResult.Success(
                new UnityRequestResponse(
                    IpcPayloadCodec.SerializeToElement(response),
                    []),
                start);
        });
        var postconditionStore = new TestMutationReadPostconditionStore();
        var service = CreateService(context, requestExecutor, postconditionStore);

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            ExecutionId,
            result.ErrorOutput!.LifecycleExecutionRef!.Id);
        Assert.Equal(
            ExecutionApplicationState.Unknown,
            result.ErrorOutput.ApplicationState);
        Assert.Contains(
            result.Failures,
            static failure =>
                failure.Message.Contains(
                    "Lifecycle Execution",
                    StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_WhenTerminalPublicationFails_RetainsPublishingReference ()
    {
        var context =
            ProjectContextTestFactory.CreateRepositoryFixtureProject();
        var registration = new LifecycleExecutionRegistration(
            new LifecycleExecutionDefinition(
                LifecycleExecutionKind.Refresh),
            ExecutionId,
            StartedAtUtc.AddMinutes(42),
            StartedAtUtc.AddMinutes(-3));
        var start = CreateStartBinding(context, registration);
        var publishingReference = new RecoveryExecutionRef(
            start.LifecycleExecutionRef.Kind,
            start.LifecycleExecutionRef.Id,
            start.LifecycleExecutionRef.DefinitionDigest,
            new ExecutionState(TextVocabulary.GetText(
                LifecycleExecutionState.Publishing)),
            start.LifecycleExecutionRef.StatusLocator);
        var reconnectResolver =
            new RecordingLifecycleExecutionReconnectResolver(
                new LifecycleExecutionReconnectResolution.PublicationFailed(
                    ApplicationFailure.InternalError(
                        "Terminal Record publication failed.",
                        LifecycleExecutionErrorCodes.TerminalPublicationFailed),
                    publishingReference));
        var requestExecutor = new RecordingUnityRequestExecutor(
            _ => throw new InvalidOperationException(
                "A failed Terminal Record publication must not dispatch the refresh provider."));
        var service = CreateService(
            context,
            requestExecutor,
            new TestMutationReadPostconditionStore(),
            reconnectResolver,
            new UnexpectedGuidGenerator());

        var result = await service.ReconnectAsync(
            RequestId,
            CreateInput(),
            start.LifecycleExecutionRef,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            LifecycleExecutionErrorCodes.TerminalPublicationFailed,
            Assert.Single(result.Failures).Code);
        Assert.Same(
            publishingReference,
            result.ErrorOutput!.LifecycleExecutionRef);
        Assert.Equal(
            ExecutionApplicationState.Indeterminate,
            result.ErrorOutput.ApplicationState);
        Assert.Empty(requestExecutor.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_UsesOriginalRegistrationAndCollectsTerminalThroughRefreshHandler ()
    {
        var context = ProjectContextTestFactory.CreateRepositoryFixtureProject();
        var originalRegistration = new LifecycleExecutionRegistration(
            new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh),
            ExecutionId,
            StartedAtUtc.AddMinutes(42),
            StartedAtUtc.AddMinutes(-3));
        var start = CreateStartBinding(context, originalRegistration);
        var publishedReference = start.LifecycleExecutionRef;
        var reconnectResolver = new RecordingLifecycleExecutionReconnectResolver(
            new LifecycleExecutionReconnectResolution.Open(
                originalRegistration,
                publishedReference,
                start));
        var requestExecutor = new RecordingUnityRequestExecutor(payload =>
        {
            var refresh = Assert.IsType<UnityRequestPayload.Refresh>(payload);
            var result = new RefreshLifecycleResult(
                new RefreshLifecycleResult.RefreshEvidence(
                    StartedAtUtc,
                    StartedAtUtc.AddSeconds(2),
                    domainReloadGenerationBefore: 1,
                    domainReloadGenerationAfter: 2),
                CreateObservation(
                    context.UnityProject.ProjectFingerprint,
                    domainReloadGeneration: 2),
                readPostcondition: null);
            return UnityRequestExecutionResult.Success(
                new UnityRequestResponse(
                    IpcPayloadCodec.SerializeToElement(
                        new IpcRefreshResponse(
                            CreateIpcProject(context),
                            CreateTerminalReference(
                                refresh.Registration,
                                completed: true),
                            result)),
                    []));
        });
        var service = CreateService(
            context,
            requestExecutor,
            new TestMutationReadPostconditionStore(),
            reconnectResolver,
            new UnexpectedGuidGenerator());

        var result = await service.ReconnectAsync(
            RequestId,
            CreateInput(),
            publishedReference,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ExecutionId, result.Output!.LifecycleExecutionRef.Id);
        var invocation = Assert.Single(requestExecutor.Invocations);
        var payload = Assert.IsType<UnityRequestPayload.Refresh>(
            invocation.Payload);
        Assert.Same(originalRegistration, payload.Registration);
        Assert.Same(start, payload.RequiredStart);
        Assert.Equal(TimeSpan.FromMilliseconds(4234), invocation.Timeout);
        Assert.Null(payload.StartAdmissionPolicy);
        var resolution = Assert.Single(reconnectResolver.Invocations);
        Assert.Equal(
            LifecycleExecutionKind.Refresh,
            resolution.ExpectedDefinition.Kind);
        Assert.Same(publishedReference, resolution.ExecutionRef);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_WithCompletedTerminalRecord_ReplaysRefreshResultWithoutProviderDispatch ()
    {
        var context =
            ProjectContextTestFactory.CreateRepositoryFixtureProject();
        var registration = new LifecycleExecutionRegistration(
            new LifecycleExecutionDefinition(
                LifecycleExecutionKind.Refresh),
            ExecutionId,
            StartedAtUtc.AddMinutes(42),
            StartedAtUtc.AddMinutes(-3));
        var start = CreateStartBinding(context, registration);
        var readPostcondition =
            CreateReadPostcondition(StartedAtUtc);
        var typedResult = new RefreshLifecycleResult(
            new RefreshLifecycleResult.RefreshEvidence(
                StartedAtUtc,
                StartedAtUtc.AddSeconds(2),
                domainReloadGenerationBefore: 1,
                domainReloadGenerationAfter: 2),
            CreateObservation(
                context.UnityProject.ProjectFingerprint,
                domainReloadGeneration: 2),
            readPostcondition);
        var terminalReference = CreateTerminalReference(
            registration,
            completed: true);
        var terminalRecord = new RefreshLifecycleExecutionTerminalRecord(
            ExecutionId,
            start.LifecycleExecutionRef.DefinitionDigest,
            start.Project,
            start.Host,
            start.StartedGeneration,
            typedResult.Lifecycle.State.Generations,
            start.DeadlineUtc,
            start.StartedAtUtc,
            StartedAtUtc.AddSeconds(2),
            LifecycleExecutionTerminalReason.Completed,
            ExecutionApplicationState.Applied,
            typedResult,
            verdict: null,
            Array.Empty<ArtifactRef>());
        var reconnectResolver =
            new RecordingLifecycleExecutionReconnectResolver(
                new LifecycleExecutionReconnectResolution.Terminal(
                    terminalReference,
                    terminalRecord));
        var requestExecutor = new RecordingUnityRequestExecutor(
            _ => throw new InvalidOperationException(
                "A completed refresh Terminal Record must not reconnect through a provider."));
        var postconditionStore =
            new TestMutationReadPostconditionStore();
        var service = CreateService(
            context,
            requestExecutor,
            postconditionStore,
            reconnectResolver,
            new UnexpectedGuidGenerator());

        var result = await service.ReconnectAsync(
            RequestId,
            CreateInput(),
            terminalReference,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(
            terminalReference,
            result.Output!.LifecycleExecutionRef);
        Assert.Equal(typedResult.Refresh, result.Output.Refresh);
        Assert.Single(postconditionStore.WriteInvocations);
        Assert.Empty(requestExecutor.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_WhenCallerCancellationRacesDispatch_InvalidatesReadsAndRetainsOriginalReference ()
    {
        var context = ProjectContextTestFactory.CreateRepositoryFixtureProject();
        var originalRegistration = new LifecycleExecutionRegistration(
            new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh),
            ExecutionId,
            StartedAtUtc.AddMinutes(42),
            StartedAtUtc.AddMinutes(-3));
        var start = CreateStartBinding(context, originalRegistration);
        var reconnectResolver = new RecordingLifecycleExecutionReconnectResolver(
            new LifecycleExecutionReconnectResolution.Open(
                originalRegistration,
                start.LifecycleExecutionRef,
                start));
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Failure(
                new UnityRequestFailure(
                    UnityRequestFailureKind.General,
                    ExecutionErrorCodes.Canceled,
                    "Waiting for reconnected refresh was canceled."),
                start,
                lifecycleActionDispatched: false));
        var postconditionStore = new TestMutationReadPostconditionStore();
        var service = CreateService(
            context,
            requestExecutor,
            postconditionStore,
            reconnectResolver,
            new UnexpectedGuidGenerator());

        var result = await service.ReconnectAsync(
            RequestId,
            CreateInput(),
            start.LifecycleExecutionRef,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionId, result.ErrorOutput!.LifecycleExecutionRef!.Id);
        Assert.Equal(
            ExecutionApplicationState.Indeterminate,
            result.ErrorOutput.ApplicationState);
        Assert.Equal(
            ExecutionErrorCodes.Canceled,
            Assert.Single(result.Failures).Code);
        Assert.Equal(
            ApplicationFailureKind.Canceled,
            Assert.Single(result.Failures).Kind);
        Assert.Single(postconditionStore.WriteInvocations);
        var registration = Assert.IsType<UnityRequestPayload.Refresh>(
            Assert.Single(requestExecutor.Invocations).Payload).Registration;
        Assert.Same(originalRegistration, registration);
    }

    private static RefreshService CreateService (
        ProjectContext context,
        RecordingUnityRequestExecutor requestExecutor,
        TestMutationReadPostconditionStore postconditionStore,
        ILifecycleExecutionReconnectResolver? reconnectResolver = null,
        IGuidGenerator? executionIdGenerator = null,
        ILifecycleExecutionHostExitTerminalizer? hostExitTerminalizer = null,
        TimeProvider? timeProvider = null)
    {
        var resolvedTimeProvider =
            timeProvider ?? new FakeTimeProvider(StartedAtUtc);
        return new RefreshService(
            new StaticProjectContextResolver(ProjectContextResolutionResult.Success(context)),
            requestExecutor,
            postconditionStore,
            reconnectResolver
                ?? new UnexpectedLifecycleExecutionReconnectResolver(),
            hostExitTerminalizer
                ?? new UnexpectedLifecycleExecutionHostExitTerminalizer(),
            new LifecycleExecutionRegistrationIssuer(
                executionIdGenerator
                    ?? new StaticGuidGenerator(ExecutionId),
                resolvedTimeProvider),
            resolvedTimeProvider);
    }

    private static RefreshCommandInput CreateInput ()
    {
        return new RefreshCommandInput(
            ProjectPath: ProjectContextTestFactory.UnityProjectRoot,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 1234,
            FailFast: true);
    }

    private static LifecycleExecutionStartBinding CreateStartBinding (
        ProjectContext context,
        LifecycleExecutionRegistration registration)
    {
        return new LifecycleExecutionStartBinding(
            new ActiveExecutionRef(
                registration.Definition.ExecutionKind,
                registration.ExecutionId,
                LifecycleExecutionDefinitionDigest.Calculate(registration.Definition),
                new ExecutionState(TextVocabulary.GetText(LifecycleExecutionState.Registered)),
                new ExecutionStatusLocator(
                    $".ucli/local/lifecycle-executions/{registration.ExecutionId:N}/execution.json")),
            CreateIpcProject(context),
            CreateHost(),
            new UnityEditorGenerationSnapshot(0, 1, 1, 0),
            registration.DeadlineUtc,
            registration.StartedAtUtc);
    }

    private static TerminalExecutionRef CreateTerminalReference (
        LifecycleExecutionRegistration registration,
        bool completed,
        Guid? executionId = null)
    {
        var actualExecutionId = executionId ?? registration.ExecutionId;
        return new TerminalExecutionRef(
            registration.Definition.ExecutionKind,
            actualExecutionId,
            LifecycleExecutionDefinitionDigest.Calculate(registration.Definition),
            new ExecutionState(TextVocabulary.GetText(
                completed
                    ? LifecycleExecutionState.Completed
                    : LifecycleExecutionState.Failed)),
            statusLocator: null,
            new PathArtifactRef(
                LifecycleExecutionArtifactContract.TerminalRecordKind,
                LifecycleExecutionArtifactContract.TerminalRecordMediaType,
                new ArtifactPath(
                    $".ucli/local/artifacts/lifecycle-execution/refresh/{actualExecutionId:N}/terminal.json"),
                Sha256Digest.Parse(new string('a', 64)),
                sizeBytes: 123,
                StartedAtUtc.AddSeconds(2)));
    }

    private static RecoveryExecutionRef CreatePublishingReference (
        LifecycleExecutionRegistration registration)
    {
        return new RecoveryExecutionRef(
            registration.Definition.ExecutionKind,
            registration.ExecutionId,
            LifecycleExecutionDefinitionDigest.Calculate(registration.Definition),
            new ExecutionState(TextVocabulary.GetText(
                LifecycleExecutionState.Publishing)),
            new ExecutionStatusLocator(
                $".ucli/local/lifecycle-executions/{registration.ExecutionId:N}/execution.json"));
    }

    private static LifecycleExecutionHostRegistration CreateHost ()
    {
        return new LifecycleExecutionHostRegistration(
            new ProcessIdentity(42, 123456),
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            Guid.Parse("11111111-2222-3333-4444-555555555555"));
    }

    private static UnityProjectIdentity CreateIpcProject (ProjectContext context)
    {
        return new UnityProjectIdentity(
            context.UnityProject.UnityProjectRoot.Value,
            context.UnityProject.ProjectFingerprint,
            context.UnityProject.UnityVersion);
    }

    private static UnityEditorObservation CreateObservation (
        ProjectFingerprint fingerprint,
        long domainReloadGeneration)
    {
        return UnityEditorObservationTestFactory.Create(
            projectFingerprint: fingerprint,
            generations: new UnityEditorGenerationSnapshot(
                CompileGeneration: 0,
                DomainReloadGeneration: domainReloadGeneration,
                AssetRefreshGeneration: 1,
                PlayModeGeneration: 0),
            observedAtUtc: StartedAtUtc.AddSeconds(2));
    }

    private static ExecutionReadPostcondition CreateReadPostcondition (
        DateTimeOffset minSafeGeneratedAtUtc)
    {
        return new ExecutionReadPostcondition(
        [
            new ExecutionReadPostconditionRequirement(
                ExecutionReadPostconditionSurface.AssetSearch,
                minSafeGeneratedAtUtc,
                ScenePath: null),
        ]);
    }
}
