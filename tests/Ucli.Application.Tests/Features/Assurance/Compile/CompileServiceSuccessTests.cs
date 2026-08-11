using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Contracts.Execution;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Compile.CompileServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Compile;

public sealed class CompileServiceSuccessTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task StartAsync_UsesTheCallerFixedBindingAndReverifiesTheTerminalRecord ()
    {
        var compileResult = CreateResult();
        var requestExecutor = new RecordingUnityRequestExecutor(
            CreateCompileResponseResult(compileResult));
        var reconnectResolver = new RecordingLifecycleExecutionReconnectResolver(
            CreateTerminalResolution(compileResult, Verdict.Pass));
        var service = CreateService(
            unityRequestExecutor: requestExecutor,
            reconnectResolver: reconnectResolver);
        var progress = new CollectingCommandProgressSink();

        var result = await service.StartAsync(
            await CreateStartInvocationAsync(requestExecutor), progress);

        var completed = Assert.IsType<CompileExecutionResult.CompletedResult>(result);
        Assert.Equal(Verdict.Pass, completed.Output.Verdict);
        Assert.Equal(ExecutionId, completed.Output.LifecycleExecutionRef.Id);
        var provider = Assert.Single(requestExecutor.Invocations);
        Assert.Equal(UcliCommandIds.Compile, provider.Command);
        Assert.Equal(TimeSpan.FromSeconds(13), provider.Timeout);
        Assert.IsType<UnityRequestPayload.Compile>(provider.Payload);
        Assert.Single(reconnectResolver.Invocations);
        EventSequenceAssert.EmittedEventsInOrder(
            progress.Entries,
            CompileProgressEventNames.Started,
            CompileProgressEventNames.Completed);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task StartAsync_WithCompilerErrors_UsesTheTerminalVerdict ()
    {
        var compileResult = CreateResult(errorCount: 1);
        var requestExecutor = new RecordingUnityRequestExecutor(
            CreateCompileResponseResult(compileResult));
        var service = CreateService(
            unityRequestExecutor: requestExecutor,
            reconnectResolver: new RecordingLifecycleExecutionReconnectResolver(
                CreateTerminalResolution(compileResult, Verdict.Fail)));

        var result = await service.StartAsync(
            await CreateStartInvocationAsync(requestExecutor));

        var completed = Assert.IsType<CompileExecutionResult.CompletedResult>(result);
        Assert.Equal(Verdict.Fail, completed.Output.Verdict);
        Assert.Equal(1, completed.Output.Compile.ScriptCompilation.Diagnostics.ErrorCount);
    }
}
