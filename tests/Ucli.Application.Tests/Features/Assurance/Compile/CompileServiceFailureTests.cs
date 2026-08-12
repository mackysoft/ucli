using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Compile.CompileServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Compile;

public sealed class CompileServiceFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task StartAsync_WhenTheExecutionDeadlineAlreadyElapsed_DoesNotDispatchTheFixedHost ()
    {
        var timeProvider = new FakeTimeProvider(StartedAtUtc);
        var requestExecutor = new RecordingUnityRequestExecutor(
            _ => throw new InvalidOperationException("An elapsed deadline must not dispatch."));
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider);
        var context = ProjectContextTestFactory.CreateSingleRootProject();
        var binding = (await requestExecutor.BindAsync(
            UnityExecutionMode.Oneshot,
            context.UnityProject,
            deadline)).Binding!;
        var service = CreateService(
            unityRequestExecutor: requestExecutor,
            timeProvider: timeProvider);
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        var result = await service.StartAsync(new LifecycleExecutionStartInvocation(
            new LifecycleExecutionFixedContext(
                context,
                UnityExecutionMode.Oneshot,
                binding),
            deadline,
            deadline,
            NullLifecycleExecutionStartObserver.Instance));

        var failed = Assert.IsType<CompileExecutionResult.FailedResult>(result);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, failed.Failure.Code);
        Assert.Empty(requestExecutor.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartAsync_WithInvalidResponseAfterDurableStart_RetainsTheRegisteredReferenceAsUnknown ()
    {
        using var document = JsonDocument.Parse("""{"result":null}""");
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                new UnityRequestResponse(document.RootElement.Clone(), []),
                CreateStart()));
        var service = CreateService(unityRequestExecutor: requestExecutor);

        var result = await service.StartAsync(
            await CreateStartInvocationAsync(requestExecutor));

        var failed = Assert.IsType<CompileExecutionResult.FailedResult>(result);
        Assert.Equal(ExecutionId, failed.LifecycleExecutionRef!.Id);
        Assert.Equal(ExecutionApplicationState.Unknown, failed.ApplicationState);
        Assert.Contains("Unity compile payload is invalid.", failed.Failure.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartAsync_WhenTheFixedHostExited_PublishesTheUnityExitedTerminal ()
    {
        var start = CreateStart();
        var terminalReference = CreateFailedTerminalReference();
        var terminalizer = new RecordingLifecycleExecutionHostExitTerminalizer(
            new LifecycleExecutionHostExitTerminalizationResult.Published(
                terminalReference,
                new CompileLifecycleExecutionTerminalRecord(
                    ExecutionId,
                    start.LifecycleExecutionRef.DefinitionDigest,
                    start.Project,
                    start.Host,
                    start.StartedGeneration,
                    terminalGeneration: null,
                    start.DeadlineUtc,
                    start.StartedAtUtc,
                    StartedAtUtc.AddSeconds(1),
                    LifecycleExecutionTerminalReason.UnityExited,
                    ExecutionApplicationState.NotApplied,
                    result: null,
                    verdict: null,
                    Array.Empty<ArtifactRef>())));
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Failure(
                new UnityRequestFailure(
                    UnityRequestFailureKind.General,
                    EditorLifecycleErrorCodes.EditorUnavailable,
                    "The fixed Unity host exited."),
                start,
                lifecycleActionDispatched: false,
                new LifecycleExecutionHostExitObservation(start.Host.Process)));
        var service = CreateService(
            unityRequestExecutor: requestExecutor,
            hostExitTerminalizer: terminalizer,
            timeProvider: new FakeTimeProvider(StartedAtUtc.AddSeconds(1)));

        var result = await service.StartAsync(
            await CreateStartInvocationAsync(requestExecutor));

        var failed = Assert.IsType<CompileExecutionResult.FailedResult>(result);
        Assert.Equal(LifecycleExecutionErrorCodes.UnityExited, failed.Failure.Code);
        Assert.Same(terminalReference, failed.LifecycleExecutionRef);
        Assert.Single(terminalizer.Invocations);
    }
}
