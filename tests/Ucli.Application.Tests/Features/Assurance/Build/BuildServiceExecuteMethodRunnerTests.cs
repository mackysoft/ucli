using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildServiceExecuteMethodRunnerTestSupport;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildServiceExecuteMethodRunnerTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithExecuteMethodRunner_ResolvesInvocationIntoInternalIpcRequest ()
    {
        const string EnvironmentValue = "release";
        const string SecretValue = "secret-value";
        const string profilePath = "/workspace/build.ucli.json";
        var profileJson = CreateExecuteMethodProfileJson(
            method: "Build.Entry.Run",
            arguments: """
                      "run": "${ucli.build.runId}",
                      "output": "${ucli.build.outputDir}",
                      "profile": "${ucli.build.profilePath}",
                      "digest": "${ucli.build.profileDigest}",
                      "project": "${project.path}",
                      "fingerprint": "${project.fingerprint}",
                      "target": "${build.target}"
                """,
            environmentVariables: """
                    "UCLI_MODE"
                """,
            environment: """
                    "UCLI_SECRET"
                """);
        using var tempDirectory = CreateArtifactDirectoryScope();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.FullPath);
        var requestExecutor = CreateBuildResponseExecutor(
            ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
            errorCount: 0,
            runnerResult: CreateExecuteMethodRunnerResult(),
            omitReport: true);
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, profilePath)),
            environmentVariableReader: new StubEnvironmentVariableReader(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["UCLI_MODE"] = EnvironmentValue,
                ["UCLI_SECRET"] = SecretValue,
            }),
            requestExecutor: requestExecutor,
            artifactStore: artifactStore);
        var progressSink = new CollectingCommandProgressSink();

        var result = await service.ExecuteAsync(CreateInput(), progressSink);

        if (!result.IsSuccess)
        {
            Assert.Fail(string.Join(Environment.NewLine, result.Errors.Select(static error => $"{error.Code}: {error.Message}")));
        }

        var outputDirectory = artifactStore.PreparedPaths!.RunnerOutputDirectory;
        var profileDigest = BuildProfileResolver.ResolveJson(profileJson).Profile!.Digest;
        BuildRunInvocationAssert.ExecuteMethodRunnerRequest(
            requestExecutor,
            expectedRunId: RunId,
            expectedProfilePath: profilePath,
            expectedProfileDigest: profileDigest,
            expectedOutputDirectory: outputDirectory,
            expectedProjectPath: "/workspace/UnityProject",
            expectedProjectFingerprint: ProjectFingerprint,
            expectedBuildTarget: "standaloneLinux64",
            expectedEnvironmentVariable: "UCLI_MODE",
            expectedEnvironmentValue: EnvironmentValue,
            expectedEnvironmentSecret: "UCLI_SECRET",
            expectedSecretValue: SecretValue);
        var accountingRequest = Assert.IsType<BuildRunArtifactAccountingRequest>(artifactStore.AccountingRequest);
        Assert.Null(accountingRequest.BuildReport);
        var outputSource = Assert.Single(accountingRequest.OutputSources);
        Assert.True(outputSource.IsRunnerOutputRelative);
        Assert.Equal("player.txt", outputSource.Path);
        Assert.False(accountingRequest.AllowEmptyOutputManifest);

        var output = result.Output!;
        Assert.Equal("executeMethod", output.Build.Runner.Kind);
        Assert.Equal("Build.Entry.Run", output.Build.Runner.Method);
        Assert.Equal("ucliBuildRunnerResult", output.Build.RunnerResult.Source);
        Assert.Equal(output.Build.RunnerResult.Status, output.Build.Summary.Result);
        Assert.Equal(["UCLI_MODE"], output.Build.Runner.Invocation.Environment.Variables);
        Assert.Equal(["UCLI_SECRET"], output.Build.Runner.Invocation.Environment.Secrets);
        Assert.Null(output.Build.Summary.ReportRef);
        Assert.False(output.Reports.ContainsKey(BuildReportRefs.BuildReport));
        Assert.DoesNotContain(output.Claims, static claim => claim.Id == BuildClaimCodes.UnityBuildReportAccounted.Value);
        Assert.DoesNotContain(EnvironmentValue, JsonSerializer.Serialize(output, PayloadSerializerOptions));
        Assert.DoesNotContain(SecretValue, JsonSerializer.Serialize(output, PayloadSerializerOptions));
        var semanticResult = CreateBuildSemanticInvariantValidator().Validate(JsonSerializer.SerializeToElement(output, PayloadSerializerOptions));
        Assert.True(semanticResult.IsValid, string.Join(Environment.NewLine, semanticResult.Violations.Select(static violation => $"{violation.Path}: {violation.Message}")));
        Assert.NotNull(artifactStore.WrittenMetadata);
        Assert.DoesNotContain(EnvironmentValue, JsonSerializer.Serialize(artifactStore.WrittenMetadata!, PayloadSerializerOptions));
        Assert.DoesNotContain(SecretValue, JsonSerializer.Serialize(artifactStore.WrittenMetadata!, PayloadSerializerOptions));
        Assert.DoesNotContain(EnvironmentValue, JsonSerializer.Serialize(progressSink.Entries, PayloadSerializerOptions));
        Assert.DoesNotContain(SecretValue, JsonSerializer.Serialize(progressSink.Entries, PayloadSerializerOptions));
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
        BuildProgressAssert.ExecuteMethodRunnerKindPreserved(progressSink);
        Assert.Equal("executeMethod", artifactStore.WrittenMetadata!.Runner.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, artifactStore.WrittenMetadata.Runner.GetProperty("outputLayout").ValueKind);
        Assert.Equal(output.Build.RunnerResult.Source, artifactStore.WrittenMetadata.RunnerResult.GetProperty("source").GetString());
        Assert.Equal(output.Build.RunnerResult.Status, artifactStore.WrittenMetadata.RunnerResult.GetProperty("status").GetString());
        Assert.Equal(
            ["UCLI_MODE"],
            artifactStore.WrittenMetadata.Runner.GetProperty("invocation").GetProperty("environment").GetProperty("variables").EnumerateArray().Select(static item => item.GetString()!).ToArray());
        Assert.Equal(
            ["UCLI_SECRET"],
            artifactStore.WrittenMetadata.Runner.GetProperty("invocation").GetProperty("environment").GetProperty("secrets").EnumerateArray().Select(static item => item.GetString()!).ToArray());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithExecuteMethodRunnerBuildReportPath_AccountsOptionalBuildReport ()
    {
        var profileJson = CreateExecuteMethodProfileJson(
            method: "Build.Entry.Run",
            arguments: string.Empty,
            environment: string.Empty);
        using var tempDirectory = CreateArtifactDirectoryScope();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.FullPath);
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, "/workspace/build.ucli.json")),
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                runnerResult: CreateExecuteMethodRunnerResult(buildReport: new IpcBuildRunnerResultBuildReport("reports/build-report.json")),
                omitReport: true),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(static error => $"{error.Code}: {error.Message}")));
        var accountingRequest = Assert.IsType<BuildRunArtifactAccountingRequest>(artifactStore.AccountingRequest);
        Assert.NotNull(accountingRequest.BuildReport);
        Assert.Equal("reports/build-report.json", accountingRequest.BuildReport.RunnerOutputRelativePath);
        var output = result.Output!;
        Assert.Equal(BuildReportRefs.BuildReport, output.Build.Summary.ReportRef);
        Assert.True(output.Reports.ContainsKey(BuildReportRefs.BuildReport));
        var reportClaim = Assert.Single(output.Claims, static claim => claim.Id == BuildClaimCodes.UnityBuildReportAccounted.Value);
        Assert.False(reportClaim.Required);
        var semanticResult = CreateBuildSemanticInvariantValidator().Validate(JsonSerializer.SerializeToElement(output, PayloadSerializerOptions));
        Assert.True(semanticResult.IsValid, string.Join(Environment.NewLine, semanticResult.Violations.Select(static violation => $"{violation.Path}: {violation.Message}")));
    }
}
