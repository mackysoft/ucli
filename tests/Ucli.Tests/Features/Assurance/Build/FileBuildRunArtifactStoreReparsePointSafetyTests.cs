using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Tests.Features.Assurance.Build.FileBuildRunArtifactStoreTestSupport;

namespace MackySoft.Ucli.Tests.Features.Assurance.Build;

public sealed class FileBuildRunArtifactStoreReparsePointSafetyTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenOutputContainsSymbolicLink_ReturnsOutputManifestFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "output-symlink");
        var (store, paths) = PrepareArtifacts(scope);
        var targetPath = scope.WriteFile("target.txt", "linked output");
        var linkPath = Path.Combine(paths.RunnerOutputDirectory, "build");
        if (!TryCreateFileSymbolicLink(linkPath, targetPath))
        {
            return;
        }

        var writeResult = await store.AccountArtifactsAsync(
            CreateAccountingRequest(paths),
            CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildOutputManifestFailed, error.Code);
        Assert.Contains("reparse point", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenOutputSourceContainsSymbolicLinkAncestor_ReturnsOutputManifestFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "output-symlink-ancestor");
        var (store, paths) = PrepareArtifacts(scope);
        var outputSourceDirectory = Path.Combine(paths.RunnerOutputDirectory, "build");
        Directory.CreateDirectory(outputSourceDirectory);
        var targetDirectory = scope.CreateDirectory("outside-output");
        var targetFilePath = Path.Combine(targetDirectory, "payload.txt");
        WriteUtf8(targetFilePath, "external output");
        var linkPath = Path.Combine(outputSourceDirectory, "linked");
        if (!TryCreateDirectorySymbolicLink(linkPath, targetDirectory))
        {
            return;
        }

        var writeResult = await store.AccountArtifactsAsync(
            CreateAccountingRequest(paths, Path.Combine(linkPath, "payload.txt")),
            CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildOutputManifestFailed, error.Code);
        Assert.Contains("reparse point", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenBuildReportSourceContainsSymbolicLinkAncestor_ReturnsBuildReportMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "build-report-symlink-ancestor");
        var (store, paths) = PrepareArtifacts(scope);
        var outputSourcePath = Path.Combine(paths.RunnerOutputDirectory, "build");
        WriteUtf8(outputSourcePath, "player output");
        var targetDirectory = scope.CreateDirectory("outside-output");
        var targetBuildReportPath = Path.Combine(targetDirectory, "build-report.json");
        WriteUtf8(
            targetBuildReportPath,
            IpcPayloadCodec.SerializeToElement(CreateBuildReportArtifact(paths)).GetRawText());
        var reportDirectory = Path.Combine(paths.RunnerOutputDirectory, "reports");
        Directory.CreateDirectory(reportDirectory);
        var linkPath = Path.Combine(reportDirectory, "linked");
        if (!TryCreateDirectorySymbolicLink(linkPath, targetDirectory))
        {
            return;
        }

        var writeResult = await store.AccountArtifactsAsync(
            CreateAccountingRequest(
                paths,
                BuildReportSourceEntry.FromRunnerOutputRelativePath(
                    new BuildRunnerOutputPath("reports/linked/build-report.json")),
                outputSourcePath),
            CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildReportMissing, error.Code);
        Assert.Contains("reparse point", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenRunnerOutputRootIsSymbolicLink_ReturnsBuildReportMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "build-report-symlink-root");
        using var targetScope = TestDirectories.CreateTempScope("build-artifact-store", "build-report-symlink-root-target");
        var (store, paths) = PrepareArtifacts(scope);
        var targetOutputRoot = targetScope.CreateDirectory("output");
        WriteUtf8(
            Path.Combine(targetOutputRoot, "reports", "build-report.json"),
            IpcPayloadCodec.SerializeToElement(CreateBuildReportArtifact(paths)).GetRawText());
        Directory.Delete(paths.RunnerOutputDirectory);
        if (!TryCreateDirectorySymbolicLink(paths.RunnerOutputDirectory, targetOutputRoot))
        {
            return;
        }

        var writeResult = await store.AccountArtifactsAsync(
            CreateBuildReportOnlyAccountingRequest(paths, "reports/build-report.json"),
            CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildReportMissing, error.Code);
        Assert.Contains("reparse point", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
