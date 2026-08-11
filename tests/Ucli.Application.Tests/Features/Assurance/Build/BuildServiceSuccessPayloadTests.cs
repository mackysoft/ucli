using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildServiceTestSupport;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildServiceSuccessPayloadTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithSucceededBuildReport_ReturnsArtifactBackedPayload ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.FullPath);
        var requestExecutor = CreateBuildResponseExecutor(
            IpcBuildReportResult.Succeeded,
            IpcBuildLogCompletionReason.Completed,
            errorCount: 0);
        var progressSink = new CollectingCommandProgressSink();
        var service = CreateService(
            requestExecutor: requestExecutor,
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput(), progressSink);

        var completed = Assert.IsType<BuildExecutionResult.CompletedResult>(result);
        var output = completed.Output;
        Assert.Equal(Verdict.Pass, output.Verdict);
        Assert.Equal(RunId, output.Build.RunId);
        Assert.Equal(IpcBuildReportResult.Succeeded, output.Build.Summary.Result);
        Assert.Equal(IpcBuildRunnerResultSource.BuildPipelineBuildReport, output.Build.RunnerResult.Source);
        Assert.Equal(output.Build.RunnerResult.Status, output.Build.Summary.Result);
        Assert.Equal(BuildArtifactKind.BuildReport, output.Build.Summary.ReportRef);
        Assert.Equal(BuildArtifactKind.BuildLog, output.Build.Logs.ReportRef);
        Assert.Equal(IpcBuildLogCompletionReason.Completed, output.Build.Logs.CompletionReason);
        var generationsBefore = Assert.IsType<UnityEditorGenerationSnapshot>(output.Build.Generations.Before);
        var generationsAfter = Assert.IsType<UnityEditorGenerationSnapshot>(output.Build.Generations.After);
        var generationsValidFor = Assert.IsType<UnityEditorGenerationSnapshot>(output.Build.Generations.ValidFor);
        Assert.Equal(10, generationsBefore.AssetRefreshGeneration);
        Assert.Equal(11, generationsAfter.AssetRefreshGeneration);
        Assert.Equal(11, generationsValidFor.AssetRefreshGeneration);
        var expectedProfileDigest = BuildProfileResolver.ResolveJson(ProfileJson).Profile!.Digest;
        Assert.Equal(expectedProfileDigest, output.Build.Profile.Digest);
        Assert.Equal(BuildProfileInputsKind.Explicit, output.Build.Inputs.InputKind);
        Assert.Equal(BuildTargetStableName.StandaloneLinux64, output.Build.Inputs.Target.StableName);
        Assert.Equal("StandaloneLinux64", output.Build.Inputs.Target.UnityBuildTarget);
        Assert.Equal(BuildProfileSceneSource.Explicit, output.Build.Inputs.Scenes.Source);
        Assert.Equal([new SceneAssetPath("Assets/Scenes/Main.unity")], output.Build.Inputs.Scenes.Paths);
        Assert.True(output.Build.Inputs.Options.Development);
        Assert.Null(output.Build.Inputs.UnityBuildProfile);
        Assert.Equal(BuildArtifactKind.BuildOutputManifest, output.Build.Output.ManifestRef);
        Assert.Equal(StubBuildRunArtifactStore.OutputManifestDigest, output.Build.Output.ManifestDigest);
        Assert.Equal(1, output.Build.Output.EntryCount);
        Assert.Equal(1, output.Build.Output.FileCount);
        Assert.Equal(StubBuildRunArtifactStore.BuildMetadataDigest, output.Reports.Build.Digest);
        Assert.Equal(StubBuildRunArtifactStore.BuildReportArtifactDigest, output.Reports.BuildReport!.Digest);
        Assert.Equal(StubBuildRunArtifactStore.BuildOutputManifestArtifactDigest, output.Reports.BuildOutputManifest.Digest);
        Assert.Equal(StubBuildRunArtifactStore.BuildLogArtifactDigest, output.Reports.BuildLog.Digest);
        Assert.Equal("build.json", output.Reports.Build.Path);
        Assert.Equal("build-report.json", output.Reports.BuildReport.Path);
        Assert.Equal("output-manifest.json", output.Reports.BuildOutputManifest.Path);
        Assert.Equal("build.log", output.Reports.BuildLog.Path);
        Assert.Equal(BuildArtifactKind.BuildOutputManifest, output.Build.Output.ManifestRef);
        AssertEvidenceRefsResolveToReports(output);
        Assert.DoesNotContain(output.Claims, static claim => claim.Id == BuildClaimCodes.UnityBuildExecuteMethodResolved);
        Assert.DoesNotContain(output.Claims, static claim => claim.Id == BuildClaimCodes.UnityBuildExecuteMethodInvoked);
        Assert.DoesNotContain(output.Claims, static claim => claim.Id == BuildClaimCodes.UnityBuildExecuteMethodCompleted);
        Assert.All(output.Claims, claim => Assert.True(claim.Required));
        var verifier = Assert.Single(output.Verifiers);
        Assert.Equal(new AssuranceVerifierId("build"), verifier.Id);
        Assert.Equal(output.Claims.Where(static claim => claim.Required).Select(static claim => claim.Id).ToArray(), verifier.PrimaryClaims);
        Assert.Equal(BuildPipelineEffectValues, verifier.Effects);
        var preparedPaths = artifactStore.PreparedPaths;
        Assert.NotNull(preparedPaths);
        Assert.NotNull(artifactStore.WrittenMetadata);
        Assert.Equal(
            TextVocabulary.GetText(IpcBuildReportResult.Succeeded),
            artifactStore.WrittenMetadata!.Summary.GetProperty("result").GetString());
        Assert.Equal(
            TextVocabulary.GetText(output.Build.RunnerResult.Source),
            artifactStore.WrittenMetadata.RunnerResult.GetProperty("source").GetString());
        Assert.Equal(
            TextVocabulary.GetText(output.Build.RunnerResult.Status),
            artifactStore.WrittenMetadata.RunnerResult.GetProperty("status").GetString());
        Assert.Equal(output.Build.Profile.Path, artifactStore.WrittenMetadata.Profile.GetProperty("path").GetString());
        Assert.Equal(expectedProfileDigest.ToString(), artifactStore.WrittenMetadata.Profile.GetProperty("digest").GetString());
        Assert.Equal(
            TextVocabulary.GetText(output.Build.Inputs.InputKind),
            artifactStore.WrittenMetadata.Inputs.GetProperty("inputKind").GetString());
        Assert.Equal(
            TextVocabulary.GetText(output.Build.Inputs.Target.StableName),
            artifactStore.WrittenMetadata.Inputs.GetProperty("target").GetProperty("stableName").GetString());
        Assert.Equal(
            output.Build.Inputs.Target.UnityBuildTarget,
            artifactStore.WrittenMetadata.Inputs.GetProperty("target").GetProperty("unityBuildTarget").GetString());
        Assert.Equal(
            TextVocabulary.GetText(output.Build.Inputs.Scenes.Source),
            artifactStore.WrittenMetadata.Inputs.GetProperty("scenes").GetProperty("source").GetString());
        Assert.Equal(
            output.Build.Inputs.Options.Development,
            artifactStore.WrittenMetadata.Inputs.GetProperty("options").GetProperty("development").GetBoolean());
        Assert.False(artifactStore.WrittenMetadata.Inputs.TryGetProperty("unityBuildProfile", out _));
        Assert.Equal("buildPipeline", artifactStore.WrittenMetadata.Runner.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, artifactStore.WrittenMetadata.Runner.GetProperty("method").ValueKind);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("{}"),
            JsonNode.Parse(artifactStore.WrittenMetadata.Runner
                .GetProperty("invocation")
                .GetProperty("arguments")
                .GetRawText())));
        var runnerEnvironment = artifactStore.WrittenMetadata.Runner.GetProperty("invocation").GetProperty("environment");
        Assert.Equal(0, runnerEnvironment.GetProperty("variables").GetArrayLength());
        Assert.Equal(0, runnerEnvironment.GetProperty("secrets").GetArrayLength());
        Assert.Equal("file", artifactStore.WrittenMetadata.Runner.GetProperty("outputLayout").GetProperty("shape").GetString());
        Assert.Equal(
            CreateExpectedPlayerLocationPathName(preparedPaths.RunnerOutputDirectory.Value),
            artifactStore.WrittenMetadata.Runner.GetProperty("outputLayout").GetProperty("locationPathName").GetString());
        Assert.Equal(TextVocabulary.GetText(output.Build.Summary.ReportRef!.Value), artifactStore.WrittenMetadata.Summary.GetProperty("reportRef").GetString());
        Assert.Equal(TextVocabulary.GetText(output.Build.Logs.ReportRef), artifactStore.WrittenMetadata.Logs.GetProperty("reportRef").GetString());
        Assert.False(artifactStore.WrittenMetadata.ProjectMutation.GetProperty("mutated").GetBoolean());
        Assert.Equal("full", artifactStore.WrittenMetadata.ProjectMutation.GetProperty("coverage").GetString());
        Assert.Equal(output.Build.Generations.Before.CompileGeneration, artifactStore.WrittenMetadata.Generations.GetProperty("before").GetProperty("compileGeneration").GetInt64());
        Assert.Equal(output.Build.Generations.After.DomainReloadGeneration, artifactStore.WrittenMetadata.Generations.GetProperty("after").GetProperty("domainReloadGeneration").GetInt64());
        Assert.Equal(output.Build.Generations.ValidFor.AssetRefreshGeneration, artifactStore.WrittenMetadata.Generations.GetProperty("validFor").GetProperty("assetRefreshGeneration").GetInt64());
        Assert.Equal("ready", artifactStore.WrittenMetadata.Lifecycle.GetProperty("before").GetProperty("state").GetProperty("lifecycleState").GetString());
        Assert.Equal("ready", artifactStore.WrittenMetadata.Lifecycle.GetProperty("after").GetProperty("state").GetProperty("lifecycleState").GetString());
        EventSequenceAssert.EmittedEventsInOrder(
            progressSink.Entries,
            BuildRunProgressEventNames.Started,
            BuildRunProgressEventNames.ReadinessCompleted,
            BuildRunProgressEventNames.RunnerResolved,
            BuildRunProgressEventNames.RunnerStarted,
            BuildRunProgressEventNames.RunnerCompleted,
            BuildRunProgressEventNames.RunnerResultCompleted,
            BuildRunProgressEventNames.ArtifactsCompleted,
            BuildRunProgressEventNames.Completed);
        BuildProgressAssert.BuildPipelineSuccessProgressPayloads(
            progressSink,
            expectedRunId: RunId,
            expectedProfileDigest: expectedProfileDigest);

        var requestPayload = BuildRunInvocationAssert.ExplicitBuildPipelineRequest(
            requestExecutor,
            expectedRunId: RunId,
            expectedRunnerOutputDirectory: preparedPaths.RunnerOutputDirectory.Value,
            expectedBuildReportPath: preparedPaths.BuildReportJsonPath.Value,
            expectedBuildLogPath: preparedPaths.BuildLogPath.Value,
            expectedLocationPathName: CreateExpectedPlayerLocationPathName(preparedPaths.RunnerOutputDirectory.Value));
        Assert.NotEqual(preparedPaths.RunnerOutputDirectory, preparedPaths.ArtifactOutputDirectory);
        var accountingRequest = Assert.IsType<BuildRunArtifactAccountingRequest>(artifactStore.AccountingRequest);
        var outputSource = Assert.Single(accountingRequest.OutputSources);
        var absoluteOutputSource = Assert.IsType<BuildOutputSourceEntry.Absolute>(outputSource);
        Assert.Equal(
            AbsolutePath.Parse(requestPayload.Request.OutputLayout!.LocationPathName).Value,
            absoluteOutputSource.Path.Value);
        Assert.Equal(BuildTargetStableName.StandaloneLinux64, accountingRequest.BuildTarget);
        Assert.Equal("StandaloneLinux64", accountingRequest.UnityBuildTarget);
        Assert.False(accountingRequest.AllowEmptyOutputManifest);
    }
}
