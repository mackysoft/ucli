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
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                runnerResult: new IpcBuildRunnerResultArtifact(
                    Source: ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.UcliBuildRunnerResult),
                    Status: ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                    DurationMilliseconds: 2500,
                    ErrorCount: 0,
                    WarningCount: 0,
                    Diagnostics: [])),
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
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                runnerResult: new IpcBuildRunnerResultArtifact(
                    Source: ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.BuildPipelineBuildReport),
                    Status: ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                    DurationMilliseconds: 9999,
                    ErrorCount: 0,
                    WarningCount: 0,
                    Diagnostics: [])),
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
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Unknown),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Failed),
                errorCount: 0),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData("unsupported", "Assets/Scenes/Main.unity")]
    [InlineData("explicit", " Assets/Scenes/Main.unity")]
    public async Task Execute_WithInvalidResolvedInputResponse_ReturnsCommandFailure (
        string sceneSource,
        string scenePath)
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                sceneSource: sceneSource,
                scenes: [scenePath]),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData("otherTarget", "StandaloneLinux64", "Development")]
    [InlineData("standaloneLinux64", "StandaloneOSX", "Development")]
    [InlineData("standaloneLinux64", "StandaloneLinux64", "None")]
    public async Task Execute_WithMismatchedResolvedInputResponse_ReturnsCommandFailure (
        string buildTarget,
        string unityBuildTarget,
        string buildOptions)
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
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
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                scenes: ["Assets/Scenes/Other.unity"]),
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
                    Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                    LocationPathName: Path.Combine(tempDirectory.FullPath, "other-output", "player", "Player"));
                return CreateBuildResponseResult(
                    ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                    ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                    errorCount: 0,
                    reportOutputPath: buildRunPayload.OutputLayout!.LocationPathName,
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
    public async Task Execute_WithMismatchedCompletionReasonResponse_ReturnsCommandFailure ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Failed),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
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
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Unknown),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Failed),
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
