using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Vocabulary;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Assurance;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Compile.CompileServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Compile;

public sealed class CompileServiceRecoveryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithRecoveredArtifactRunIdMismatch_ReturnsCommandFailure ()
    {
        var service = CreateService(
            unityRequestExecutor: new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
                UnityRequestFailureKind.General,
                ExecutionErrorCodes.IpcTimeout,
                "Unity compile request timed out."))),
            artifactStore: new StubCompileRunArtifactStore(CompileRunArtifactReadResult.Success(CreateSummary(runId: "other-run"))));

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("runId mismatch", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithCompileResponseMissingSummary_ReturnsCommandFailure ()
    {
        using var document = JsonDocument.Parse("""{"runId":"run-1","summary":null}""");
        var payload = document.RootElement.Clone();
        var service = CreateService(
            unityRequestExecutor: new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(new UnityRequestResponse(
                payload,
                [],
                HasFailureStatus: false))));

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("summary", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithCompileTimeoutResponse_ReadsRecoveredArtifact ()
    {
        var artifactStore = new StubCompileRunArtifactStore(CompileRunArtifactReadResult.Success(CreateSummary(errorCount: 1)));
        var progressSink = new CollectingCommandProgressSink();
        var service = CreateService(
            unityRequestExecutor: new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(new UnityRequestResponse(
                default,
                [new OperationExecutionError(ExecutionErrorCodes.IpcTimeout, "Unity compile assurance timed out.", null)],
                HasFailureStatus: true))),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000), progressSink);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, artifactStore.ReadCount);
        Assert.Equal(CompileVerdictValues.Fail, result.Output!.Verdict);
        EventSequenceAssert.EmittedEventsInOrder(
            progressSink.Entries,
            CompileProgressEventNames.Started,
            CompileProgressEventNames.RefreshStarted,
            CompileProgressEventNames.Recovered,
            CompileProgressEventNames.Completed);
        CompileProgressAssert.TimeoutRecoveredArtifactProgressPayload(
            progressSink,
            "/workspace/.ucli/local/compile/run-1/summary.json");

        var resultWithoutProgress = await CreateService(
                unityRequestExecutor: new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(new UnityRequestResponse(
                    default,
                    [new OperationExecutionError(ExecutionErrorCodes.IpcTimeout, "Unity compile assurance timed out.", null)],
                    HasFailureStatus: true))),
                artifactStore: new StubCompileRunArtifactStore(CompileRunArtifactReadResult.Success(CreateSummary(errorCount: 1))))
            .ExecuteAsync(new CompileCommandInput(
                ProjectPath: null,
                Mode: UnityExecutionMode.Oneshot,
                TimeoutMilliseconds: 10000));
        AssertCompileOutputsMatch(result.Output!, resultWithoutProgress.Output!);
    }

    private static void AssertCompileOutputsMatch (
        CompileExecutionOutput expected,
        CompileExecutionOutput actual)
    {
        Assert.Equal(expected.Verdict, actual.Verdict);
        Assert.Equal(expected.Claims.Select(static claim => claim.Id), actual.Claims.Select(static claim => claim.Id));
        Assert.Equal(expected.Claims.Select(static claim => claim.Status), actual.Claims.Select(static claim => claim.Status));
        Assert.Equal(
            expected.Reports.OrderBy(static entry => entry.Key, StringComparer.Ordinal),
            actual.Reports.OrderBy(static entry => entry.Key, StringComparer.Ordinal));
        Assert.Equal(expected.ResidualRisks, actual.ResidualRisks);
    }
}
