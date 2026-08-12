using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using static MackySoft.Ucli.Application.Tests.Play.PlayExitServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayExitServiceReconnectTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_UsesTheFixedBindingForAnOpenExecution ()
    {
        var start = CreateStartBinding();
        var registration = new LifecycleExecutionRegistration(
            new LifecycleExecutionDefinition(LifecycleExecutionKind.PlayExit),
            ExecutionId,
            start.DeadlineUtc,
            start.StartedAtUtc);
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateResponse(CreateExitedResponse()),
                start));
        var resolver = new RecordingLifecycleExecutionReconnectResolver(
            new LifecycleExecutionReconnectResolution.Open(
                registration,
                start.LifecycleExecutionRef,
                start));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor,
            resolver,
            new UnexpectedGuidGenerator());

        var result = await service.ReconnectAsync(
            await CreateReconnectInvocationAsync(
                requestExecutor,
                start.LifecycleExecutionRef));

        Assert.True(result.IsSuccess, result.Error?.Message);
        var payload = Assert.IsType<UnityRequestPayload.PlayExit>(
            Assert.Single(requestExecutor.Invocations).Payload);
        Assert.Same(registration, payload.Registration);
        Assert.Same(start, payload.RequiredStart);
        Assert.Single(resolver.Invocations);
    }
}
