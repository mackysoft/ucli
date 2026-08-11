using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Compile.CompileServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Compile;

public sealed class CompileServiceReconnectTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_WithTerminalRecord_DoesNotDispatchAnotherProviderAction ()
    {
        var requestExecutor = new RecordingUnityRequestExecutor(
            _ => throw new InvalidOperationException("Terminal execution must not be dispatched."));
        var terminalReference = CreateTerminalReference();
        var service = CreateService(
            unityRequestExecutor: requestExecutor,
            reconnectResolver: new RecordingLifecycleExecutionReconnectResolver(
                CreateTerminalResolution(CreateResult(), Verdict.Pass)),
            executionIdGenerator: new UnexpectedGuidGenerator());

        var result = await service.ReconnectAsync(
            await CreateReconnectInvocationAsync(requestExecutor, terminalReference));

        Assert.IsType<CompileExecutionResult.CompletedResult>(result);
        Assert.Empty(requestExecutor.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_UsesTheCallerFixedBindingForTheOpenExecution ()
    {
        var start = CreateStart();
        var registration = new LifecycleExecutionRegistration(
            new LifecycleExecutionDefinition(LifecycleExecutionKind.Compile),
            ExecutionId,
            start.DeadlineUtc,
            start.StartedAtUtc);
        var requestExecutor = new RecordingUnityRequestExecutor(
            CreateCompileResponseResult(CreateResult()));
        var resolver = new RecordingLifecycleExecutionReconnectResolver(
            new LifecycleExecutionReconnectResolution.Open(
                registration,
                start.LifecycleExecutionRef,
                start),
            CreateTerminalResolution(CreateResult(), Verdict.Pass));
        var service = CreateService(
            unityRequestExecutor: requestExecutor,
            reconnectResolver: resolver,
            executionIdGenerator: new UnexpectedGuidGenerator());

        var result = await service.ReconnectAsync(
            await CreateReconnectInvocationAsync(
                requestExecutor,
                start.LifecycleExecutionRef));

        Assert.IsType<CompileExecutionResult.CompletedResult>(result);
        var payload = Assert.IsType<UnityRequestPayload.Compile>(
            Assert.Single(requestExecutor.Invocations).Payload);
        Assert.Same(registration, payload.Registration);
        Assert.Same(start, payload.RequiredStart);
        Assert.Equal(2, resolver.Invocations.Count);
    }
}
