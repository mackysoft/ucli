using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Assurance;

namespace MackySoft.Ucli.Tests;

public sealed class BuildRunCommandResultFactoryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithFailedVerifierVerdict_ReturnsOkStatusAndExitCodeOne ()
    {
        var output = CreateOutput(ContractLiteralCodec.ToValue(BuildVerdict.Fail));

        var result = BuildRunCommandResultFactory.Create(BuildExecutionResult.Success(output));

        Assert.Equal(IpcProtocol.StatusOk, result.Status);
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Errors);
        Assert.Same(output, result.Payload);
    }

    private static BuildExecutionOutput CreateOutput (string verdict)
    {
        var project = new ProjectIdentityInfo(
            ProjectPath: "/workspace/UnityProject",
            ProjectFingerprint: "project-fingerprint",
            UnityVersion: "6000.1.4f1");
        var build = new BuildOutput(
            RunId: "build-run-1",
            Profile: new BuildProfileOutput("/workspace/build.ucli.json", "profile-digest"),
            Target: "standaloneLinux64",
            Scenes: new BuildScenesOutput("explicit", ["Assets/Scenes/Main.unity"]),
            Options: new BuildOptionsOutput(Development: true),
            Output: new BuildArtifactOutput(
                Kind: "ucliArtifact",
                ArtifactRoot: "/workspace/.ucli/local/fingerprints/project-fingerprint/artifacts/build/build-run-1",
                OutputRoot: "/workspace/.ucli/local/fingerprints/project-fingerprint/artifacts/build/build-run-1/output",
                ManifestRef: "buildOutputManifest",
                ManifestDigest: "manifest-digest",
                FileCount: 0,
                TotalBytes: 0),
            Generations: new BuildGenerationsOutput(
                Before: new BuildGenerationSnapshotOutput("compile-before", "domain-before", "asset-before"),
                After: new BuildGenerationSnapshotOutput("compile-after", "domain-after", "asset-after"),
                ValidFor: new BuildGenerationSnapshotOutput("compile-after", "domain-after", "asset-after")),
            Summary: new BuildSummaryOutput(
                Result: ContractLiteralCodec.ToValue(IpcBuildReportResult.Failed),
                DurationMilliseconds: 2500,
                ErrorCount: 1,
                WarningCount: 0,
                ReportRef: "buildReport"),
            Logs: new BuildLogsOutput(
                ReportRef: "buildLog",
                EntryCount: 1,
                ErrorCount: 1,
                WarningCount: 0,
                CompletionReason: ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Failed),
                Window: new BuildLogWindowOutput(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)));
        return new BuildExecutionOutput(
            Verdict: verdict,
            Project: project,
            Build: build,
            Verifiers: [],
            Claims: [],
            Reports: new Dictionary<string, BuildReportOutput>(StringComparer.Ordinal)
            {
                ["build"] = new BuildReportOutput("build", "/workspace/build.json", "metadata-digest"),
                ["buildReport"] = new BuildReportOutput("buildReport", "/workspace/build-report.json", "build-report-digest"),
                ["buildOutputManifest"] = new BuildReportOutput("buildOutputManifest", "/workspace/output-manifest.json", "output-manifest-digest"),
                ["buildLog"] = new BuildReportOutput("buildLog", "/workspace/build.log", "build-log-digest"),
            },
            ResidualRisks: []);
    }
}
