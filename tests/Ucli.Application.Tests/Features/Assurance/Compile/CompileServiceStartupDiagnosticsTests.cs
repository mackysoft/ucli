using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Vocabulary;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Storage;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Compile.CompileServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Compile;

public sealed class CompileServiceStartupDiagnosticsTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithStartupCompilerDiagnostic_ReturnsDiagnosticsReadFailurePacket ()
    {
        var artifactStore = new StubCompileRunArtifactStore();
        var progressSink = new CollectingCommandProgressSink();
        var service = CreateService(
            unityRequestExecutor: new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
                DaemonErrorCodes.DaemonStartupBlocked,
                "Unity startup was blocked by script compilation errors.",
                CreateCompilerStartupFailure()))),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000), progressSink);

        Assert.True(result.IsSuccess);
        var output = result.Output!;
        Assert.Equal(CompileVerdictValues.Fail, output.Verdict);
        Assert.Equal("diagnosticsRead", output.Compile.Refresh.Origin);
        Assert.False(output.Compile.Refresh.Requested);
        Assert.Equal(1, output.Compile.ScriptCompilation.Diagnostics.ErrorCount);
        Assert.Equal("CS0246", output.Compile.ScriptCompilation.Diagnostics.PrimaryDiagnostic!.Code);
        Assert.Null(output.Compile.Lifecycle.LifecycleState);
        Assert.Equal(0, artifactStore.ReadCount);
        Assert.Equal(1, artifactStore.WriteCount);
        Assert.Equal("diagnosticsRead", artifactStore.WrittenSummary!.Refresh.Origin);
        EventSequenceAssert.EmittedEventsInOrder(
            progressSink.Entries,
            CompileProgressEventNames.Started,
            CompileProgressEventNames.RefreshStarted,
            CompileProgressEventNames.Diagnostic,
            CompileProgressEventNames.Completed);
        CompileProgressAssert.StartupCompilerDiagnosticProgressPayload(progressSink);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithStartupCompilerDiagnosisWithoutPrimaryDiagnostic_ReturnsDiagnosticsReadFailurePacket ()
    {
        var artifactStore = new StubCompileRunArtifactStore();
        var service = CreateService(
            unityRequestExecutor: new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
                DaemonErrorCodes.DaemonStartupBlocked,
                "Unity startup was blocked by script compilation errors.",
                CreateStartupFailure(
                    DaemonDiagnosisReasonValues.UnityScriptCompilationFailed,
                    primaryDiagnostic: null)))),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000));

        Assert.True(result.IsSuccess);
        var diagnostic = result.Output!.Compile.ScriptCompilation.Diagnostics.PrimaryDiagnostic!;
        Assert.Equal("compiler", diagnostic.Kind);
        Assert.Null(diagnostic.Code);
        Assert.Contains("script compilation", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, artifactStore.ReadCount);
        Assert.Equal(1, artifactStore.WriteCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithNonCompilerStartupFailure_ReturnsFailureWithoutPollingArtifact ()
    {
        var artifactStore = new StubCompileRunArtifactStore();
        var service = CreateService(
            unityRequestExecutor: new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
                DaemonErrorCodes.DaemonStartupBlocked,
                "Unity startup was blocked by package resolution.",
                CreateStartupFailure(
                    DaemonDiagnosisReasonValues.UnityPackageResolutionFailed,
                    primaryDiagnostic: null)))),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000));

        Assert.False(result.IsSuccess);
        Assert.Equal(0, artifactStore.ReadCount);
        Assert.Equal(0, artifactStore.WriteCount);
        var error = Assert.Single(result.Errors);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, error.Code);
        Assert.NotNull(error.StartupFailure);
    }
}
