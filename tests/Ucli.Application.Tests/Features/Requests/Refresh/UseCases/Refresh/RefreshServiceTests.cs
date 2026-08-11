using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Application.Tests.Refresh;

public sealed class RefreshServiceTests
{
    private static readonly Guid RequestId = Guid.Parse("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62");

    private static readonly Guid ExecutionId = Guid.Parse("ab0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c63");

    private static readonly DateTimeOffset StartedAtUtc = new(2026, 7, 31, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartAsync_UsesTheCallerFixedBindingAndPreservesFailFast ()
    {
        var context = ProjectContextTestFactory.CreateRepositoryFixtureProject();
        var postcondition = CreateReadPostcondition();
        var requestExecutor = new RecordingUnityRequestExecutor(payload =>
        {
            var refresh = Assert.IsType<UnityRequestPayload.Refresh>(payload);
            return UnityRequestExecutionResult.Success(new UnityRequestResponse(
                IpcPayloadCodec.SerializeToElement(new IpcRefreshResponse(
                    CreateIpcProject(context),
                    CreateTerminalReference(refresh.Registration, completed: true),
                    CreateResult(context, postcondition))),
                []));
        });
        var postconditionStore = new TestMutationReadPostconditionStore();
        var service = CreateService(context, postconditionStore);

        var result = await service.StartAsync(
            RequestId,
            await CreateStartInvocationAsync(context, requestExecutor),
            failFast: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(RequestId, result.Output!.RequestId);
        var provider = Assert.Single(requestExecutor.Invocations);
        Assert.Equal(UcliCommandIds.Refresh, provider.Command);
        Assert.Equal(TimeSpan.FromMilliseconds(4234), provider.Timeout);
        Assert.True(Assert.IsType<RefreshLifecycleExecutionStartAdmissionPolicy>(
            Assert.IsType<UnityRequestPayload.Refresh>(provider.Payload).StartAdmissionPolicy).FailFast);
        Assert.Single(postconditionStore.WriteInvocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartAsync_WhenResponseIdentifiesAnotherExecution_RetainsTheDurableStart ()
    {
        var context = ProjectContextTestFactory.CreateRepositoryFixtureProject();
        var requestExecutor = new RecordingUnityRequestExecutor(payload =>
        {
            var refresh = Assert.IsType<UnityRequestPayload.Refresh>(payload);
            return UnityRequestExecutionResult.Success(
                new UnityRequestResponse(
                    IpcPayloadCodec.SerializeToElement(new IpcRefreshResponse(
                        CreateIpcProject(context),
                        CreateTerminalReference(
                            new LifecycleExecutionRegistration(
                                refresh.Registration.Definition,
                                Guid.NewGuid(),
                                refresh.Registration.DeadlineUtc,
                                refresh.Registration.StartedAtUtc),
                            completed: true),
                        CreateResult(context, readPostcondition: null))),
                    []),
                CreateStartBinding(context, refresh.Registration));
        });
        var service = CreateService(context, new TestMutationReadPostconditionStore());

        var result = await service.StartAsync(
            RequestId,
            await CreateStartInvocationAsync(context, requestExecutor),
            failFast: false);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionId, result.ErrorOutput!.LifecycleExecutionRef!.Id);
        Assert.Equal(ExecutionApplicationState.Unknown, result.ErrorOutput.ApplicationState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_WhenCallerWaitIsCanceled_RetainsTheOriginalExecutionWithoutResolvingAnotherHost ()
    {
        var context = ProjectContextTestFactory.CreateRepositoryFixtureProject();
        var registration = new LifecycleExecutionRegistration(
            new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh),
            ExecutionId,
            StartedAtUtc.AddMinutes(42),
            StartedAtUtc);
        var start = CreateStartBinding(context, registration);
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Failure(
                new UnityRequestFailure(
                    UnityRequestFailureKind.General,
                    ExecutionErrorCodes.Canceled,
                    "The caller stopped waiting."),
                start,
                lifecycleActionDispatched: false));
        var postconditionStore = new TestMutationReadPostconditionStore();
        var service = CreateService(
            context,
            postconditionStore,
            new RecordingLifecycleExecutionReconnectResolver(
                new LifecycleExecutionReconnectResolution.Open(
                    registration,
                    start.LifecycleExecutionRef,
                    start)));

        var result = await service.ReconnectAsync(
            RequestId,
            await CreateReconnectInvocationAsync(
                context,
                requestExecutor,
                start.LifecycleExecutionRef));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionId, result.ErrorOutput!.LifecycleExecutionRef!.Id);
        Assert.Equal(ExecutionApplicationState.Indeterminate, result.ErrorOutput.ApplicationState);
        Assert.Single(requestExecutor.Invocations);
        Assert.Single(postconditionStore.WriteInvocations);
    }

    private static RefreshService CreateService (
        ProjectContext context,
        TestMutationReadPostconditionStore postconditionStore,
        ILifecycleExecutionReconnectResolver? reconnectResolver = null)
    {
        var timeProvider = new FakeTimeProvider(StartedAtUtc);
        return new RefreshService(
            postconditionStore,
            reconnectResolver ?? new UnexpectedLifecycleExecutionReconnectResolver(),
            new UnexpectedLifecycleExecutionHostExitTerminalizer(),
            new LifecycleExecutionRegistrationIssuer(
                new StaticGuidGenerator(ExecutionId),
                timeProvider),
            timeProvider);
    }

    private static async ValueTask<LifecycleExecutionStartInvocation>
        CreateStartInvocationAsync (
            ProjectContext context,
            RecordingUnityRequestExecutor requestExecutor)
    {
        var timeProvider = new FakeTimeProvider(StartedAtUtc);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromMilliseconds(1234), timeProvider);
        var binding = (await requestExecutor.BindAsync(
            UnityExecutionMode.Oneshot,
            context.UnityProject,
            deadline)).Binding!;
        return new LifecycleExecutionStartInvocation(
            new LifecycleExecutionFixedContext(
                context,
                UnityExecutionMode.Oneshot,
                binding),
            deadline,
            deadline.CreateCompletionDeadline(LifecycleExecutionTiming.ResponseDeliveryGrace),
            NullLifecycleExecutionStartObserver.Instance);
    }

    private static async ValueTask<LifecycleExecutionReconnectInvocation>
        CreateReconnectInvocationAsync (
            ProjectContext context,
            RecordingUnityRequestExecutor requestExecutor,
            ExecutionRef executionReference)
    {
        var deadline = ExecutionDeadline.Start(
            TimeSpan.FromMilliseconds(4234),
            new FakeTimeProvider(StartedAtUtc));
        var binding = (await requestExecutor.BindAsync(
            UnityExecutionMode.Oneshot,
            context.UnityProject,
            deadline)).Binding!;
        return new LifecycleExecutionReconnectInvocation(
            new LifecycleExecutionFixedContext(
                context,
                UnityExecutionMode.Oneshot,
                binding),
            executionReference,
            deadline);
    }

    private static RefreshLifecycleResult CreateResult (
        ProjectContext context,
        ExecutionReadPostcondition? readPostcondition)
    {
        return new RefreshLifecycleResult(
            new RefreshLifecycleResult.RefreshEvidence(
                StartedAtUtc,
                StartedAtUtc.AddSeconds(2),
                domainReloadGenerationBefore: 1,
                domainReloadGenerationAfter: 2),
            UnityEditorObservationTestFactory.Create(
                projectFingerprint: context.UnityProject.ProjectFingerprint,
                generations: new UnityEditorGenerationSnapshot(0, 2, 1, 0),
                observedAtUtc: StartedAtUtc.AddSeconds(2)),
            readPostcondition);
    }

    private static ExecutionReadPostcondition CreateReadPostcondition () => new(
    [
        new ExecutionReadPostconditionRequirement(
            ExecutionReadPostconditionSurface.AssetSearch,
            StartedAtUtc,
            ScenePath: null),
    ]);

    private static LifecycleExecutionStartBinding CreateStartBinding (
        ProjectContext context,
        LifecycleExecutionRegistration registration) => new(
        new ActiveExecutionRef(
            registration.Definition.ExecutionKind,
            registration.ExecutionId,
            LifecycleExecutionDefinitionDigest.Calculate(registration.Definition),
            new ExecutionState(TextVocabulary.GetText(LifecycleExecutionState.Registered)),
            new ExecutionStatusLocator(
                $".ucli/local/lifecycle-executions/{registration.ExecutionId:N}/execution.json")),
        CreateIpcProject(context),
        new LifecycleExecutionHostRegistration(
            new ProcessIdentity(42, 123456),
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            Guid.Parse("11111111-2222-3333-4444-555555555555")),
        new UnityEditorGenerationSnapshot(0, 1, 1, 0),
        registration.DeadlineUtc,
        registration.StartedAtUtc);

    private static TerminalExecutionRef CreateTerminalReference (
        LifecycleExecutionRegistration registration,
        bool completed) => new(
        registration.Definition.ExecutionKind,
        registration.ExecutionId,
        LifecycleExecutionDefinitionDigest.Calculate(registration.Definition),
        new ExecutionState(TextVocabulary.GetText(
            completed ? LifecycleExecutionState.Completed : LifecycleExecutionState.Failed)),
        statusLocator: null,
        new PathArtifactRef(
            LifecycleExecutionArtifactContract.TerminalRecordKind,
            LifecycleExecutionArtifactContract.TerminalRecordMediaType,
            new ArtifactPath(
                $".ucli/local/artifacts/lifecycle-execution/refresh/{registration.ExecutionId:N}/terminal.json"),
            Sha256Digest.Parse(new string('a', 64)),
            sizeBytes: 123,
            StartedAtUtc.AddSeconds(2)));

    private static UnityProjectIdentity CreateIpcProject (ProjectContext context) => new(
        context.UnityProject.UnityProjectRoot.Value,
        context.UnityProject.ProjectFingerprint,
        context.UnityProject.UnityVersion);
}
