using System.Text.Json;
using System.Text.Json.Nodes;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildServiceResponseValidationTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithBuildPipelineRunnerResultSourceMismatch_ReturnsCommandFailure ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                IpcBuildReportResult.Succeeded,
                IpcBuildLogCompletionReason.Completed,
                errorCount: 0,
                runnerResult: new IpcBuildRunnerResultArtifact(
                    Source: IpcBuildRunnerResultSource.UcliBuildRunnerResult,
                    Status: IpcBuildReportResult.Succeeded,
                    DurationMilliseconds: 2500,
                    ErrorCount: 0,
                    WarningCount: 0,
                    Diagnostics: [],
                    Outputs: [new BuildRunnerOutputPath("player.txt")],
                    BuildReport: null)),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithBuildPipelineRunnerResultSummaryMismatch_ReturnsCommandFailure ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                IpcBuildReportResult.Succeeded,
                IpcBuildLogCompletionReason.Completed,
                errorCount: 0,
                runnerResult: new IpcBuildRunnerResultArtifact(
                    Source: IpcBuildRunnerResultSource.BuildPipelineBuildReport,
                    Status: IpcBuildReportResult.Succeeded,
                    DurationMilliseconds: 9999,
                    ErrorCount: 0,
                    WarningCount: 0,
                    Diagnostics: [],
                    Outputs: [],
                    BuildReport: null)),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithUnknownBuildPipelineReportResult_ReturnsCommandFailure ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                IpcBuildReportResult.Unknown,
                IpcBuildLogCompletionReason.Failed,
                errorCount: 0),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithUnsupportedResolvedSceneSource_ReturnsCommandFailure ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var requestExecutor = new RecordingUnityRequestExecutor(payload =>
        {
            var buildRun = (UnityRequestPayload.BuildRun)payload;
            var response = CreateBuildResponseResult(
                IpcBuildReportResult.Succeeded,
                IpcBuildLogCompletionReason.Completed,
                errorCount: 0,
                sceneSource: BuildProfileSceneSource.Explicit,
                scenes: [new SceneAssetPath("Assets/Scenes/Main.unity")],
                reportOutputPath: buildRun.Request.OutputLayout!.LocationPathName,
                outputLayout: buildRun.Request.OutputLayout).Response!;
            var payloadObject = JsonNode.Parse(response.Payload.GetRawText())!.AsObject();
            payloadObject["input"]!["sceneSource"] = "unsupported";
            return UnityRequestExecutionResult.Success(new UnityRequestResponse(
                JsonSerializer.SerializeToElement(payloadObject),
                response.Errors));
        });
        var service = CreateService(
            requestExecutor: requestExecutor,
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(BuildTargetStableName.Android, "StandaloneLinux64", "Development")]
    [InlineData(BuildTargetStableName.StandaloneLinux64, "StandaloneOSX", "Development")]
    [InlineData(BuildTargetStableName.StandaloneLinux64, "StandaloneLinux64", "None")]
    public async Task Execute_WithMismatchedResolvedInputResponse_ReturnsCommandFailure (
        BuildTargetStableName buildTarget,
        string unityBuildTarget,
        string buildOptions)
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                IpcBuildReportResult.Succeeded,
                IpcBuildLogCompletionReason.Completed,
                errorCount: 0,
                buildTarget: buildTarget,
                unityBuildTarget: unityBuildTarget,
                buildOptions: buildOptions),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithMismatchedExplicitScenesResponse_ReturnsCommandFailure ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                IpcBuildReportResult.Succeeded,
                IpcBuildLogCompletionReason.Completed,
                errorCount: 0,
                scenes: [new SceneAssetPath("Assets/Scenes/Other.unity")]),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithMismatchedOutputLayoutResponse_ReturnsCommandFailure ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.FullPath);
        var service = CreateService(
            requestExecutor: new RecordingUnityRequestExecutor(payload =>
            {
                var buildRunPayload = (UnityRequestPayload.BuildRun)payload;
                var mismatchedLayout = new IpcBuildOutputLayout(
                    Shape: IpcBuildOutputLayoutShape.File,
                    LocationPathName: Path.Combine(tempDirectory.FullPath, "other-output", "player", "Player"));
                return CreateBuildResponseResult(
                    IpcBuildReportResult.Succeeded,
                    IpcBuildLogCompletionReason.Completed,
                    errorCount: 0,
                    reportOutputPath: buildRunPayload.Request.OutputLayout!.LocationPathName,
                    outputLayout: mismatchedLayout);
            }),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Null(artifactStore.WrittenMetadata);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithRelativeOutputLayoutResponse_ReturnsCommandFailureBeforeArtifactAccounting ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.FullPath);
        var service = CreateService(
            requestExecutor: new RecordingUnityRequestExecutor(payload =>
            {
                var buildRunPayload = (UnityRequestPayload.BuildRun)payload;
                var relativeLayout = new IpcBuildOutputLayout(
                    Shape: IpcBuildOutputLayoutShape.File,
                    LocationPathName: Path.Combine("player", "Player"));
                return CreateBuildResponseResult(
                    IpcBuildReportResult.Succeeded,
                    IpcBuildLogCompletionReason.Completed,
                    errorCount: 0,
                    reportOutputPath: buildRunPayload.Request.OutputLayout!.LocationPathName,
                    outputLayout: relativeLayout);
            }),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Null(artifactStore.AccountingRequest);
        Assert.Null(artifactStore.WrittenMetadata);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithMismatchedCompletionReasonResponse_ReturnsCommandFailure ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                IpcBuildReportResult.Failed,
                IpcBuildLogCompletionReason.Completed,
                errorCount: 1),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithUnknownBuildReportResult_ReturnsCommandFailureBeforeArtifactAccounting ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.FullPath);
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                IpcBuildReportResult.Unknown,
                IpcBuildLogCompletionReason.Failed,
                errorCount: 1),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Null(artifactStore.AccountingRequest);
        Assert.Null(artifactStore.WrittenMetadata);
    }
}
