using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

public sealed class BuildRunCliOutputPayloadContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void BuildReportFailure_UsesVerifierFailureContract ()
    {
        using var document = BuildRunCliOutputContractTestSupport.CreateDocument("build-report-failed");
        var root = document.RootElement;
        var payload = root.GetProperty("payload");

        Assert.Equal(
            TextVocabulary.GetText(CommandResultStatus.Ok),
            root.GetProperty("status").GetString());
        Assert.Equal(1, root.GetProperty("exitCode").GetInt32());
        Assert.Equal(
            TextVocabulary.GetText(Verdict.Fail),
            payload.GetProperty("verdict").GetString());
        Assert.Equal(
            TextVocabulary.GetText(IpcBuildReportResult.Failed),
            payload.GetProperty("build").GetProperty("summary").GetProperty("result").GetString());
        Assert.Equal(
            TextVocabulary.GetText(IpcBuildLogCompletionReason.Failed),
            payload.GetProperty("build").GetProperty("logs").GetProperty("completionReason").GetString());
        Assert.Equal(
            TextVocabulary.GetText(AssuranceClaimStatus.Passed),
            BuildRunCliOutputContractTestSupport.GetClaimStatus(payload, BuildClaimCodes.UnityBuildCompleted.Value));
        Assert.Equal(
            TextVocabulary.GetText(AssuranceClaimStatus.Failed),
            BuildRunCliOutputContractTestSupport.GetClaimStatus(payload, BuildClaimCodes.UnityBuildSucceeded.Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void DirtySceneFailure_UsesDirtyStateContract ()
    {
        using var document = BuildRunCliOutputContractTestSupport.CreateDocument("dirty-scene");
        var root = document.RootElement;
        var payload = root.GetProperty("payload");
        var dirtyState = payload.GetProperty("dirtyState");
        var dirtyItem = dirtyState.GetProperty("items")[0];

        Assert.Equal(
            TextVocabulary.GetText(CommandResultStatus.Error),
            root.GetProperty("status").GetString());
        Assert.Equal(BuildErrorCodes.BuildDirtyStatePresent.Value, root.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.True(dirtyState.GetProperty("dirty").GetBoolean());
        Assert.Equal(
            TextVocabulary.GetText(AssuranceCoverage.Full),
            dirtyState.GetProperty("coverage").GetString());
        Assert.Equal(
            TextVocabulary.GetText(IpcBuildDirtyStateItemKind.Scene),
            dirtyItem.GetProperty("kind").GetString());
        Assert.Equal("Assets/Scenes/Main.unity", dirtyItem.GetProperty("path").GetString());
    }

    [Theory]
    [InlineData("success")]
    [InlineData("build-report-failed")]
    [Trait("Size", "Medium")]
    public void BuildRunPayloads_UseArtifactRelativeReportPaths (string caseName)
    {
        using var document = BuildRunCliOutputContractTestSupport.CreateDocument(caseName);
        var payload = document.RootElement.GetProperty("payload");
        var reports = payload.GetProperty("reports");

        foreach (var report in reports.EnumerateObject())
        {
            var path = report.Value.GetProperty("path").GetString()!;
            Assert.False(BuildRunCliOutputContractTestSupport.IsAbsoluteLikePath(path), path);
        }
    }
}
