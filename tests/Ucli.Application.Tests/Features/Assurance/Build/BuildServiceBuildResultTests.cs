using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildServiceBuildResultTests
{
    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(IpcBuildReportResult.Failed, IpcBuildLogCompletionReason.Failed)]
    [InlineData(IpcBuildReportResult.Canceled, IpcBuildLogCompletionReason.Canceled)]
    public async Task Execute_WithUnsuccessfulBuildReport_ReturnsCompletedFailVerdict (
        IpcBuildReportResult reportResult,
        IpcBuildLogCompletionReason completionReason)
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                reportResult,
                completionReason,
                errorCount: 1),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess);
        var output = result.Output!;
        Assert.Equal(AssuranceVerdict.Fail, output.Verdict);
        Assert.Equal(reportResult, output.Build.Summary.Result);
        Assert.Equal(completionReason, output.Build.Logs.CompletionReason);
        Assert.Equal(AssuranceClaimStatus.Passed, FindClaim(output, BuildClaimCodes.UnityBuildCompleted).Status);
        Assert.Equal(AssuranceClaimStatus.Failed, FindClaim(output, BuildClaimCodes.UnityBuildSucceeded).Status);
    }
}
