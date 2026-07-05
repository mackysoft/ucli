using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildServiceExecuteMethodRunnerTestSupport;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildServiceExecuteMethodRunnerArtifactValidationTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithInvalidExecuteMethodOutputPath_ReturnsBuildOutputPathInvalid ()
    {
        var result = await ExecuteWithExecuteMethodRunnerResultAsync(
            CreateExecuteMethodRunnerResult(outputs: ["../player"]),
            writeRunnerResultOutputs: false);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildOutputPathInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithMissingDeclaredExecuteMethodOutput_ReturnsBuildRunnerResultInvalid ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var artifactStore = new StubBuildRunArtifactStore(
            tempDirectory.FullPath,
            accountArtifactsOverride: static (_, _) => ValueTask.FromResult(BuildRunArtifactAccountingOperationResult.Failure(
                ExecutionError.InvalidArgument(
                    "Build runner result declared an output source that does not exist.",
                    BuildErrorCodes.BuildRunnerResultInvalid))));
        var result = await ExecuteWithExecuteMethodRunnerResultAsync(
            CreateExecuteMethodRunnerResult(outputs: ["missing-player"]),
            "completed",
            artifactStore);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerResultInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithInvalidExecuteMethodBuildReportPath_ReturnsBuildRunnerResultInvalid ()
    {
        var result = await ExecuteWithExecuteMethodRunnerResultAsync(
            CreateExecuteMethodRunnerResult(buildReport: new IpcBuildRunnerResultBuildReport("../build-report.json")),
            writeRunnerBuildReportSource: false);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerResultInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithMissingExecuteMethodBuildReportSource_ReturnsBuildReportMissing ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var artifactStore = new StubBuildRunArtifactStore(
            tempDirectory.FullPath,
            accountArtifactsOverride: static (_, _) => ValueTask.FromResult(BuildRunArtifactAccountingOperationResult.Failure(
                ExecutionError.InternalError(
                    "BuildReport source file was not found.",
                    BuildErrorCodes.BuildReportMissing))));
        var result = await ExecuteWithExecuteMethodRunnerResultAsync(
            CreateExecuteMethodRunnerResult(buildReport: new IpcBuildRunnerResultBuildReport("reports/build-report.json")),
            "completed",
            artifactStore);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildReportMissing, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithInvalidExecuteMethodBuildReportSource_ReturnsBuildReportMissing ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var artifactStore = new StubBuildRunArtifactStore(
            tempDirectory.FullPath,
            accountArtifactsOverride: static (_, _) => ValueTask.FromResult(BuildRunArtifactAccountingOperationResult.Failure(
                ExecutionError.InternalError(
                    "BuildReport source is not a valid uCLI BuildReport JSON artifact.",
                    BuildErrorCodes.BuildReportMissing))));
        var result = await ExecuteWithExecuteMethodRunnerResultAsync(
            CreateExecuteMethodRunnerResult(buildReport: new IpcBuildRunnerResultBuildReport("reports/build-report.json")),
            "completed",
            artifactStore);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildReportMissing, error.Code);
    }
}
