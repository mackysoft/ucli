using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildServiceArtifactAccountingTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenArtifactAccountingTimesOut_ReturnsIpcTimeoutFailure ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var artifactStore = new StubBuildRunArtifactStore(
            tempDirectory.FullPath,
            accountArtifactsOverride: async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Unreachable.");
            });
        var service = CreateService(artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput(timeoutMilliseconds: 50));

        var failed = Assert.IsType<BuildExecutionResult.FailedResult>(result);
        var error = failed.Failure;
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenCallerCancelsArtifactAccounting_PropagatesCancellation ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        using var cancellationTokenSource = new CancellationTokenSource();
        var artifactStore = new StubBuildRunArtifactStore(
            tempDirectory.FullPath,
            accountArtifactsOverride: async (_, cancellationToken) =>
            {
                cancellationTokenSource.Cancel();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Unreachable.");
            });
        var service = CreateService(artifactStore: artifactStore);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ExecuteAsync(CreateInput(), cancellationToken: cancellationTokenSource.Token).AsTask());
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(IpcBuildReportResult.Failed, IpcBuildLogCompletionReason.Failed)]
    [InlineData(IpcBuildReportResult.Canceled, IpcBuildLogCompletionReason.Canceled)]
    public async Task Execute_WithUnsuccessfulBuildResponse_AllowsEmptyOutputManifest (
        IpcBuildReportResult reportResult,
        IpcBuildLogCompletionReason completionReason)
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var artifactStore = new StubBuildRunArtifactStore(
            tempDirectory.FullPath,
            (request, _) =>
            {
                Assert.True(request.AllowEmptyOutputManifest);
                return ValueTask.FromResult(BuildRunArtifactAccountingOperationResult.Success(new BuildRunArtifactAccountingResult(
                    BuildReport: new BuildArtifactRef(BuildArtifactKind.BuildReport, "build-report.json", StubBuildRunArtifactStore.BuildReportArtifactDigest),
                    BuildOutputManifest: new BuildArtifactRef(BuildArtifactKind.BuildOutputManifest, "output-manifest.json", StubBuildRunArtifactStore.BuildOutputManifestArtifactDigest),
                    BuildLog: new BuildArtifactRef(BuildArtifactKind.BuildLog, "build.log", StubBuildRunArtifactStore.BuildLogArtifactDigest),
                    OutputManifest: new BuildOutputManifestSummary(
                        ManifestDigest: StubBuildRunArtifactStore.OutputManifestDigest,
                        EntryCount: 0,
                        FileCount: 0,
                        TotalBytes: 0))));
            });
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                reportResult,
                completionReason,
                errorCount: 1),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        var completed = Assert.IsType<BuildExecutionResult.CompletedResult>(result);
        Assert.Equal(0, completed.Output.Build.Output.EntryCount);
        Assert.Equal(0, completed.Output.Build.Output.FileCount);
        Assert.Equal(0, completed.Output.Build.Output.TotalBytes);
    }
}
