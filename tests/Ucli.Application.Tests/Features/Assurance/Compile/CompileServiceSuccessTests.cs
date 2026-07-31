using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Compile.CompileServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Compile;

public sealed class CompileServiceSuccessTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCallerCancelsAfterResponse_StillEmitsCompletedProjection ()
    {
        using var callerCancellation = new CancellationTokenSource();
        var unityRequestExecutor = new RecordingUnityRequestExecutor(
            CreateCompileResponseResult(CreateResult()))
        {
            OnExecute = _ => callerCancellation.Cancel(),
        };
        var progressSink = new CollectingCommandProgressSink();
        var service = CreateService(
            unityRequestExecutor: unityRequestExecutor);

        var result = await service.ExecuteAsync(
            new CompileCommandInput(
                ProjectPath: null,
                Mode: UnityExecutionMode.Auto,
                TimeoutMilliseconds: 10000),
            progressSink,
            callerCancellation.Token);

        Assert.IsType<CompileExecutionResult.CompletedResult>(result);
        EventSequenceAssert.EmittedEventsInOrder(
            progressSink.Entries,
            CompileProgressEventNames.Started,
            CompileProgressEventNames.Completed);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithSuccessfulCompileResponse_ReverifiesTerminalRecordBeforeReturningOutput ()
    {
        var compileResult = CreateResult();
        var unityRequestExecutor = new RecordingUnityRequestExecutor(
            CreateCompileResponseResult(compileResult));
        var reconnectResolver =
            new RecordingLifecycleExecutionReconnectResolver(
                CreateTerminalResolution(
                    compileResult,
                    Verdict.Pass));
        var progressSink = new CollectingCommandProgressSink();
        var service = CreateService(
            unityRequestExecutor: unityRequestExecutor,
            reconnectResolver: reconnectResolver);

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000), progressSink);

        var completed = Assert.IsType<CompileExecutionResult.CompletedResult>(result);
        var output = completed.Output;
        Assert.Equal(Verdict.Pass, output.Verdict);
        Assert.Equal(ExecutionId, output.LifecycleExecutionRef.Id);
        Assert.Equal(ExecutionLifecycle.Terminal, output.LifecycleExecutionRef.Lifecycle);
        Assert.Equal(3, output.Claims.Count);
        var invocation = Assert.Single(unityRequestExecutor.Invocations);
        Assert.Equal(UcliCommandIds.Compile, invocation.Command);
        var payload = Assert.IsType<UnityRequestPayload.Compile>(invocation.Payload);
        Assert.Equal(ExecutionId, payload.Registration.ExecutionId);
        Assert.Equal(LifecycleExecutionKind.Compile, payload.Registration.Definition.Kind);
        Assert.Equal(StartedAtUtc, payload.Registration.StartedAtUtc);
        Assert.Equal(StartedAtUtc.AddSeconds(10), payload.Registration.DeadlineUtc);
        Assert.Equal(TimeSpan.FromSeconds(13), invocation.Timeout);
        var terminalResolution = Assert.Single(reconnectResolver.Invocations);
        Assert.Equal(
            terminalResolution.ExecutionRef,
            (ExecutionRef)output.LifecycleExecutionRef);
        EventSequenceAssert.EmittedEventsInOrder(
            progressSink.Entries,
            CompileProgressEventNames.Started,
            CompileProgressEventNames.Completed);
        CompileProgressAssert.SuccessfulCompileProgressPayloads(progressSink);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithCompletedCompileContainingCompilerErrors_ReturnsFailVerdict ()
    {
        var compileResult = CreateResult(errorCount: 1);
        var service = CreateService(
            unityRequestExecutor: new RecordingUnityRequestExecutor(
                CreateCompileResponseResult(compileResult)),
            reconnectResolver:
                new RecordingLifecycleExecutionReconnectResolver(
                    CreateTerminalResolution(
                        compileResult,
                        Verdict.Fail)));

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000));

        var completed = Assert.IsType<CompileExecutionResult.CompletedResult>(result);
        Assert.Equal(Verdict.Fail, completed.Output.Verdict);
        Assert.Equal(1, completed.Output.Compile.ScriptCompilation.Diagnostics.ErrorCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithoutTimeoutOption_UsesCompileConfigOverride ()
    {
        var timeoutOverrides = new Dictionary<string, int?>(UcliConfig.CreateDefault().IpcTimeoutMillisecondsByCommand, StringComparer.Ordinal)
        {
            [UcliCommandIds.Compile.Name] = 4321,
        };
        var config = UcliConfig.CreateDefault() with
        {
            IpcTimeoutMillisecondsByCommand = timeoutOverrides,
        };
        var unityRequestExecutor = new RecordingUnityRequestExecutor(CreateCompileResponseResult(CreateResult()));
        var service = CreateService(
            projectContextResolver: new StaticProjectContextResolver(ProjectContextResolutionResult.Success(ProjectContextTestFactory.Create(
                config: config))),
            unityRequestExecutor: unityRequestExecutor,
            timeProvider: new ManualTimeProvider());

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: null));

        Assert.IsType<CompileExecutionResult.CompletedResult>(result);
        var invocation = Assert.Single(unityRequestExecutor.Invocations);
        Assert.Equal(TimeSpan.FromMilliseconds(7321), invocation.Timeout);
        var payload = Assert.IsType<UnityRequestPayload.Compile>(invocation.Payload);
        Assert.Equal(
            payload.Registration.StartedAtUtc.AddMilliseconds(4321),
            payload.Registration.DeadlineUtc);
    }
}
