using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Tests.Features.Assurance.Build.FileBuildRunArtifactStoreTestSupport;

namespace MackySoft.Ucli.Tests.Features.Assurance.Build;

public sealed class FileBuildRunArtifactStoreSpecialFileSafetyTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenBuildReportSourceIsFifo_ReturnsBuildReportMissing ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "build-report-source-fifo");
        var (store, paths) = PrepareArtifacts(scope);
        var buildReportPath = Path.Combine(paths.RunnerOutputDirectory, "reports", "build-report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(buildReportPath)!);
        if (!TryCreateFifo(buildReportPath))
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
        Assert.Contains("regular file", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenOutputContainsFifo_ReturnsOutputManifestFailed ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "output-fifo");
        var (store, paths) = PrepareArtifacts(scope);
        var fifoPath = Path.Combine(paths.RunnerOutputDirectory, "build");
        if (!TryCreateFifo(fifoPath))
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
        Assert.Contains("regular file", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenOutputFileIsUnreadable_ReturnsOutputManifestFailed ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "output-unreadable-file");
        var (store, paths) = PrepareArtifacts(scope);
        var outputPath = Path.Combine(paths.RunnerOutputDirectory, "build");
        WriteUtf8(outputPath, "secret");
        var originalMode = File.GetUnixFileMode(outputPath);
        try
        {
            File.SetUnixFileMode(outputPath, UnixFileMode.UserWrite);
            if (CanOpenForRead(outputPath))
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
        }
        finally
        {
            File.SetUnixFileMode(outputPath, originalMode);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenOutputDirectoryIsNonTraversable_ReturnsOutputManifestFailed ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "output-non-traversable-directory");
        var (store, paths) = PrepareArtifacts(scope);
        var blockedDirectory = Path.Combine(paths.RunnerOutputDirectory, "build", "blocked");
        var outputPath = Path.Combine(blockedDirectory, "payload.txt");
        WriteUtf8(outputPath, "secret");
        var originalMode = File.GetUnixFileMode(blockedDirectory);
        try
        {
            File.SetUnixFileMode(blockedDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            if (CanEnumerateDirectory(blockedDirectory))
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
        }
        finally
        {
            File.SetUnixFileMode(blockedDirectory, originalMode);
        }
    }
}
