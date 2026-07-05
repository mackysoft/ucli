using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using static MackySoft.Ucli.Contracts.Tests.Ipc.Common.IpcBuildContractSerializationTestSupport;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcBuildUnityBuildProfileContractSerializationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildRunUnityBuildProfileContracts_SerializeWithCamelCaseFields ()
    {
        var profileInput = new IpcUnityBuildProfileInput(
            Path: "Assets/BuildProfiles/Linux.asset",
            Digest: new string('f', 64),
            ApplyAudit: new IpcUnityBuildProfileApplyAudit(
                Applied: true,
                LifecycleBefore: CreateBuildLifecycleSnapshot("profile-before", canAcceptExecutionRequests: true),
                LifecycleAfter: CreateBuildLifecycleSnapshot("profile-after", canAcceptExecutionRequests: true),
                GenerationsBefore: new IpcBuildGenerationSnapshot(
                    "compile-profile-before",
                    "domain-profile-before",
                    "asset-profile-before"),
                GenerationsAfter: new IpcBuildGenerationSnapshot(
                    "compile-profile-after",
                    "domain-profile-after",
                    "asset-profile-after"),
                DirtyStateAfter: new IpcBuildDirtyState(
                    Checked: true,
                    Dirty: false,
                    Coverage: ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Full),
                    Items: [])));
        var request = IpcPayloadCodec.SerializeToElement(
            new IpcBuildRunRequest(
                RunId: "build-run-1",
                InputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile),
                BuildTarget: null,
                UnityBuildTarget: null,
                SceneSource: null,
                ScenePaths: [],
                Development: false,
                OutputPath: "/tmp/ucli/output",
                OutputLayout: null,
                BuildReportPath: "/tmp/ucli/build-report.json",
                BuildLogPath: "/tmp/ucli/build.log",
                AllowedEditorModes: ["batchmode"],
                ProjectMutationMode: "forbid",
                RunnerKind: ContractLiteralCodec.ToValue(IpcBuildRunnerKind.BuildPipeline),
                UnityBuildProfile: new IpcUnityBuildProfileInput("Assets/BuildProfiles/Linux.asset")));
        var response = IpcPayloadCodec.SerializeToElement(
            new IpcBuildRunResponse(
                RunId: "build-run-1",
                ProjectFingerprint: "project-fingerprint",
                LifecycleBefore: CreateBuildLifecycleSnapshot("before", canAcceptExecutionRequests: true),
                LifecycleAfter: CreateBuildLifecycleSnapshot("after", canAcceptExecutionRequests: true),
                DirtyState: new IpcBuildDirtyState(
                    Checked: true,
                    Dirty: false,
                    Coverage: ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Full),
                    Items: []),
                Input: new IpcBuildInputProbe(
                    InputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile),
                    BuildTarget: "standaloneLinux64",
                    UnityBuildTarget: "StandaloneLinux64",
                    UnityBuildTargetGroup: "Standalone",
                    SceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.UnityBuildProfile),
                    Scenes: ["Assets/Scenes/Main.unity"],
                    BuildOptions: "Development"),
                OutputLayout: new IpcBuildOutputLayout(
                    Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                    LocationPathName: "/tmp/ucli/output/player/Player"),
                UnityBuildProfile: profileInput,
                Report: new IpcBuildReportArtifact(
                    SchemaVersion: 1,
                    Result: ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                    UnityBuildTarget: "StandaloneLinux64",
                    OutputPath: "/tmp/ucli/output/build",
                    DurationMilliseconds: 2500,
                    TotalSizeBytes: 4096,
                    ErrorCount: 0,
                    WarningCount: 1,
                    Steps: [],
                    Messages: []),
                Logs: new IpcBuildLogSummary(
                    EntryCount: 0,
                    ErrorCount: 0,
                    WarningCount: 0,
                    CompletionReason: ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                    Window: new IpcBuildLogWindow(
                        StartedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
                        CompletedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:03+00:00"))),
                ProjectMutation: CreateProjectMutationAudit()));

        JsonAssert.For(request)
            .HasString("inputKind", ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile))
            .HasString("outputPath", "/tmp/ucli/output")
            .HasArrayLength("scenePaths", 0)
            .HasProperty("unityBuildProfile", profile => profile
                .HasString("path", "Assets/BuildProfiles/Linux.asset"));
        Assert.False(request.TryGetProperty("buildTarget", out _));
        Assert.False(request.TryGetProperty("unityBuildTarget", out _));
        Assert.False(request.TryGetProperty("sceneSource", out _));
        Assert.False(request.TryGetProperty("outputLayout", out _));
        JsonAssert.For(response)
            .HasProperty("input", input => input
                .HasString("inputKind", ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile))
                .HasString("sceneSource", ContractLiteralCodec.ToValue(BuildProfileSceneSource.UnityBuildProfile))
                .HasString("buildOptions", "Development"))
            .HasProperty("unityBuildProfile", profile => profile
                .HasString("path", "Assets/BuildProfiles/Linux.asset")
                .HasString("digest", new string('f', 64))
                .HasProperty("applyAudit", applyAudit => applyAudit
                    .HasBoolean("applied", true)
                    .HasProperty("lifecycleBefore", lifecycle => lifecycle
                        .HasString("compileGeneration", "compile-profile-before"))
                    .HasProperty("generationsAfter", generations => generations
                        .HasString("assetRefreshGeneration", "asset-profile-after"))
                    .HasProperty("dirtyStateAfter", dirty => dirty
                        .HasBoolean("checked", true)
                        .HasBoolean("dirty", false))));
    }
}
