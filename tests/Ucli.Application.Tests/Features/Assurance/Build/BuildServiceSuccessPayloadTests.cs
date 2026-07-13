using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildServiceTestSupport;

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
            ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
            errorCount: 0);
        var progressSink = new CollectingCommandProgressSink();
        var service = CreateService(
            requestExecutor: requestExecutor,
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput(), progressSink);

        if (!result.IsSuccess)
        {
            Assert.Fail(string.Join(Environment.NewLine, result.Errors.Select(static error => $"{error.Code}: {error.Message}")));
        }
        var output = result.Output!;
        Assert.Equal(ContractLiteralCodec.ToValue(BuildVerdict.Pass), output.Verdict);
        Assert.Equal(RunId, output.Build.RunId);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded), output.Build.Summary.Result);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.BuildPipelineBuildReport), output.Build.RunnerResult.Source);
        Assert.Equal(output.Build.RunnerResult.Status, output.Build.Summary.Result);
        Assert.Equal(BuildReportRefs.BuildReport, output.Build.Summary.ReportRef);
        Assert.Equal(BuildReportRefs.BuildLog, output.Build.Logs.ReportRef);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed), output.Build.Logs.CompletionReason);
        var generationsBefore = Assert.IsType<IpcUnityGenerationSnapshot>(output.Build.Generations.Before);
        var generationsAfter = Assert.IsType<IpcUnityGenerationSnapshot>(output.Build.Generations.After);
        var generationsValidFor = Assert.IsType<IpcUnityGenerationSnapshot>(output.Build.Generations.ValidFor);
        Assert.Equal(10, generationsBefore.AssetRefreshGeneration);
        Assert.Equal(11, generationsAfter.AssetRefreshGeneration);
        Assert.Equal(11, generationsValidFor.AssetRefreshGeneration);
        var expectedProfileDigest = BuildProfileResolver.ResolveJson(ProfileJson).Profile!.Digest;
        Assert.Equal(expectedProfileDigest, output.Build.Profile.Digest);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildProfileInputsKind.Explicit), output.Build.Inputs.InputKind);
        Assert.Equal("standaloneLinux64", output.Build.Inputs.Target.StableName);
        Assert.Equal("StandaloneLinux64", output.Build.Inputs.Target.UnityBuildTarget);
        Assert.Equal("explicit", output.Build.Inputs.Scenes.Source);
        Assert.Equal(["Assets/Scenes/Main.unity"], output.Build.Inputs.Scenes.Paths);
        Assert.True(output.Build.Inputs.Options.Development);
        Assert.Null(output.Build.Inputs.UnityBuildProfile);
        Assert.Equal(BuildReportRefs.BuildOutputManifest, output.Build.Output.ManifestRef);
        Assert.Equal(StubBuildRunArtifactStore.OutputManifestDigest, output.Build.Output.ManifestDigest);
        Assert.Equal(1, output.Build.Output.EntryCount);
        Assert.Equal(1, output.Build.Output.FileCount);
        Assert.Equal(
            [BuildReportRefs.Build, BuildReportRefs.BuildLog, BuildReportRefs.BuildOutputManifest, BuildReportRefs.BuildReport],
            output.Reports.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(StubBuildRunArtifactStore.BuildMetadataDigest, output.Reports[BuildReportRefs.Build].Digest);
        Assert.Equal(StubBuildRunArtifactStore.BuildReportArtifactDigest, output.Reports[BuildReportRefs.BuildReport].Digest);
        Assert.Equal(StubBuildRunArtifactStore.BuildOutputManifestArtifactDigest, output.Reports[BuildReportRefs.BuildOutputManifest].Digest);
        Assert.Equal(StubBuildRunArtifactStore.BuildLogArtifactDigest, output.Reports[BuildReportRefs.BuildLog].Digest);
        Assert.Equal("build.json", output.Reports[BuildReportRefs.Build].Path);
        Assert.Equal("build-report.json", output.Reports[BuildReportRefs.BuildReport].Path);
        Assert.Equal("output-manifest.json", output.Reports[BuildReportRefs.BuildOutputManifest].Path);
        Assert.Equal("build.log", output.Reports[BuildReportRefs.BuildLog].Path);
        Assert.True(output.Reports.ContainsKey(output.Build.Output.ManifestRef));
        AssertEvidenceRefsResolveToReports(output);
        Assert.DoesNotContain(output.Claims, static claim => claim.Id == BuildClaimCodes.UnityBuildExecuteMethodResolved.Value);
        Assert.DoesNotContain(output.Claims, static claim => claim.Id == BuildClaimCodes.UnityBuildExecuteMethodInvoked.Value);
        Assert.DoesNotContain(output.Claims, static claim => claim.Id == BuildClaimCodes.UnityBuildExecuteMethodCompleted.Value);
        Assert.All(output.Claims, claim => Assert.True(claim.Required));
        var verifier = Assert.Single(output.Verifiers);
        Assert.Equal("build", verifier.Id);
        Assert.Equal(output.Claims.Where(static claim => claim.Required).Select(static claim => claim.Id).ToArray(), verifier.PrimaryClaims);
        Assert.Equal(BuildPipelineEffectValues, verifier.Effects);
        var preparedPaths = artifactStore.PreparedPaths;
        Assert.NotNull(preparedPaths);
        Assert.NotNull(artifactStore.WrittenMetadata);
        Assert.Equal(
            ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            artifactStore.WrittenMetadata!.Summary.GetProperty("result").GetString());
        Assert.Equal(
            output.Build.RunnerResult.Source,
            artifactStore.WrittenMetadata.RunnerResult.GetProperty("source").GetString());
        Assert.Equal(
            output.Build.RunnerResult.Status,
            artifactStore.WrittenMetadata.RunnerResult.GetProperty("status").GetString());
        Assert.Equal(output.Build.Profile.Path, artifactStore.WrittenMetadata.Profile.GetProperty("path").GetString());
        Assert.Equal(expectedProfileDigest, artifactStore.WrittenMetadata.Profile.GetProperty("digest").GetString());
        Assert.Equal(output.Build.Inputs.InputKind, artifactStore.WrittenMetadata.Inputs.GetProperty("inputKind").GetString());
        Assert.Equal(
            output.Build.Inputs.Target.StableName,
            artifactStore.WrittenMetadata.Inputs.GetProperty("target").GetProperty("stableName").GetString());
        Assert.Equal(
            output.Build.Inputs.Target.UnityBuildTarget,
            artifactStore.WrittenMetadata.Inputs.GetProperty("target").GetProperty("unityBuildTarget").GetString());
        Assert.Equal(
            output.Build.Inputs.Scenes.Source,
            artifactStore.WrittenMetadata.Inputs.GetProperty("scenes").GetProperty("source").GetString());
        Assert.Equal(
            output.Build.Inputs.Options.Development,
            artifactStore.WrittenMetadata.Inputs.GetProperty("options").GetProperty("development").GetBoolean());
        Assert.False(artifactStore.WrittenMetadata.Inputs.TryGetProperty("unityBuildProfile", out _));
        Assert.Equal("buildPipeline", artifactStore.WrittenMetadata.Runner.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, artifactStore.WrittenMetadata.Runner.GetProperty("method").ValueKind);
        Assert.Equal("{}", artifactStore.WrittenMetadata.Runner.GetProperty("invocation").GetProperty("arguments").GetRawText());
        var runnerEnvironment = artifactStore.WrittenMetadata.Runner.GetProperty("invocation").GetProperty("environment");
        Assert.Equal(0, runnerEnvironment.GetProperty("variables").GetArrayLength());
        Assert.Equal(0, runnerEnvironment.GetProperty("secrets").GetArrayLength());
        Assert.Equal("file", artifactStore.WrittenMetadata.Runner.GetProperty("outputLayout").GetProperty("shape").GetString());
        Assert.Equal(
            CreateExpectedPlayerLocationPathName(preparedPaths.RunnerOutputDirectory),
            artifactStore.WrittenMetadata.Runner.GetProperty("outputLayout").GetProperty("locationPathName").GetString());
        Assert.Equal(output.Build.Summary.ReportRef, artifactStore.WrittenMetadata.Summary.GetProperty("reportRef").GetString());
        Assert.Equal(output.Build.Logs.ReportRef, artifactStore.WrittenMetadata.Logs.GetProperty("reportRef").GetString());
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

        var validator = CreateBuildSemanticInvariantValidator();
        var semanticPayload = JsonSerializer.SerializeToElement(output, PayloadSerializerOptions);
        var semanticResult = validator.Validate(semanticPayload);
        Assert.True(semanticResult.IsValid, string.Join(Environment.NewLine, semanticResult.Violations.Select(static violation => $"{violation.Path}: {violation.Message}")));

        var requestPayload = BuildRunInvocationAssert.ExplicitBuildPipelineRequest(
            requestExecutor,
            expectedRunId: RunId,
            expectedRunnerOutputDirectory: preparedPaths.RunnerOutputDirectory,
            expectedBuildReportPath: preparedPaths.BuildReportJsonPath,
            expectedBuildLogPath: preparedPaths.BuildLogPath,
            expectedLocationPathName: CreateExpectedPlayerLocationPathName(preparedPaths.RunnerOutputDirectory));
        Assert.NotEqual(preparedPaths.RunnerOutputDirectory, preparedPaths.ArtifactOutputDirectory);
        var accountingRequest = Assert.IsType<BuildRunArtifactAccountingRequest>(artifactStore.AccountingRequest);
        var outputSource = Assert.Single(accountingRequest.OutputSources);
        Assert.False(outputSource.IsRunnerOutputRelative);
        Assert.Equal(requestPayload.OutputLayout!.LocationPathName, outputSource.Path);
        Assert.Equal("standaloneLinux64", accountingRequest.BuildTarget);
        Assert.Equal("StandaloneLinux64", accountingRequest.UnityBuildTarget);
        Assert.False(accountingRequest.AllowEmptyOutputManifest);
    }
}
