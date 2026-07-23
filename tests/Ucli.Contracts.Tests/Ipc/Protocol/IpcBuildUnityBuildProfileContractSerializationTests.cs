using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using static MackySoft.Ucli.Contracts.Tests.Ipc.Common.IpcBuildContractSerializationTestSupport;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcBuildUnityBuildProfileContractSerializationTests
{
    private static readonly Guid RunId = Guid.Parse("a7de3be0-34b3-42bc-9188-9d295db8ffb6");

    [Fact]
    [Trait("Size", "Small")]
    public void IpcUnityBuildProfileInput_WithNullPath_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() => new IpcUnityBuildProfileInput(
            Path: null!,
            Digest: null,
            ApplyAudit: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcUnityBuildProfileInput_WhenOptionalWireFieldsAreMissing_DeserializesAsNull ()
    {
        var input = JsonSerializer.Deserialize<IpcUnityBuildProfileInput>(
            """
            {
              "path": "Assets/BuildProfiles/Linux.asset"
            }
            """,
            IpcJsonSerializerOptions.Default);

        Assert.NotNull(input);
        Assert.Null(input.Digest);
        Assert.Null(input.ApplyAudit);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildRunUnityBuildProfileContracts_SerializeWithCamelCaseFields ()
    {
        var profileInput = new IpcUnityBuildProfileInput(
            Path: new UnityBuildProfileAssetPath("Assets/BuildProfiles/Linux.asset"),
            Digest: Sha256Digest.Parse(new string('f', 64)),
            ApplyAudit: new IpcUnityBuildProfileApplyAudit(
                Applied: true,
                LifecycleBefore: CreateBuildLifecycleSnapshot(20, canAcceptExecutionRequests: true),
                LifecycleAfter: CreateBuildLifecycleSnapshot(21, canAcceptExecutionRequests: true),
                DirtyStateAfter: new IpcBuildDirtyState(
                    Dirty: false,
                    Coverage: IpcBuildDirtyStateCoverage.Full,
                    Items: [])));
        var request = IpcPayloadCodec.SerializeToElement(
            new IpcBuildRunRequest(
                RunId: RunId,
                InputKind: BuildProfileInputsKind.UnityBuildProfile,
                BuildTarget: null,
                SceneSource: null,
                ScenePaths: [],
                Development: false,
                OutputPath: "/tmp/ucli/output",
                OutputLayout: null,
                BuildReportPath: "/tmp/ucli/build-report.json",
                BuildLogPath: "/tmp/ucli/build.log",
                AllowedEditorModes: [DaemonEditorMode.Batchmode],
                ProjectMutationMode: BuildProfileProjectMutationMode.Forbid,
                RunnerKind: BuildRunnerKind.BuildPipeline,
                ProfileDigest: Sha256Digest.Parse(new string('c', 64)),
                UnityBuildProfile: new IpcUnityBuildProfileInput(
                    Path: new UnityBuildProfileAssetPath("Assets/BuildProfiles/Linux.asset"),
                    Digest: null,
                    ApplyAudit: null),
                ProfilePath: null,
                RunnerMethod: null,
                RunnerArguments: new Dictionary<string, string>(),
                RunnerEnvironmentVariables: [],
                RunnerEnvironmentSecrets: [],
                RunnerEnvironmentVariableValues: new Dictionary<string, string>(),
                RunnerEnvironmentSecretValues: new Dictionary<string, string>()));
        var response = IpcPayloadCodec.SerializeToElement(
            new IpcBuildRunResponse(
                RunId: RunId,
                ProjectFingerprint: TestProjectFingerprint,
                LifecycleBefore: CreateBuildLifecycleSnapshot(10, canAcceptExecutionRequests: true),
                LifecycleAfter: CreateBuildLifecycleSnapshot(11, canAcceptExecutionRequests: true),
                DirtyState: new IpcBuildDirtyState(
                    Dirty: false,
                    Coverage: IpcBuildDirtyStateCoverage.Full,
                    Items: []),
                Input: new IpcBuildInputProbe(
                    InputKind: BuildProfileInputsKind.UnityBuildProfile,
                    BuildTarget: BuildTargetStableName.StandaloneLinux64,
                    UnityBuildTarget: "StandaloneLinux64",
                    UnityBuildTargetGroup: "Standalone",
                    SceneSource: BuildProfileSceneSource.UnityBuildProfile,
                    Scenes: [new SceneAssetPath("Assets/Scenes/Main.unity")],
                    BuildOptions: "Development"),
                OutputLayout: new IpcBuildOutputLayout(
                    Shape: IpcBuildOutputLayoutShape.File,
                    LocationPathName: "/tmp/ucli/output/player/Player"),
                UnityBuildProfile: profileInput,
                Report: new IpcBuildReportArtifact(
                    SchemaVersion: 1,
                    Result: IpcBuildReportResult.Succeeded,
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
                    CompletionReason: IpcBuildLogCompletionReason.Completed,
                    Window: new IpcBuildLogWindow(
                        StartedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
                        CompletedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:03+00:00"),
                        CursorStart: null,
                        CursorEnd: null)),
                ProjectMutation: CreateProjectMutationAudit(),
                RunnerResult: null));

        JsonAssert.For(request)
            .HasString("inputKind", TextVocabulary.GetText(BuildProfileInputsKind.UnityBuildProfile))
            .HasString("outputPath", "/tmp/ucli/output")
            .HasArrayLength("scenePaths", 0)
            .HasProperty("unityBuildProfile", profile => profile
                .HasString("path", "Assets/BuildProfiles/Linux.asset"));
        Assert.False(request.TryGetProperty("buildTarget", out _));
        Assert.False(request.TryGetProperty("unityBuildTarget", out _));
        Assert.False(request.TryGetProperty("sceneSource", out _));
        Assert.False(request.TryGetProperty("outputLayout", out _));
        JsonAssert.For(response)
            .HasString("projectFingerprint", TestProjectFingerprint.ToString())
            .HasProperty("input", input => input
                .HasString("inputKind", TextVocabulary.GetText(BuildProfileInputsKind.UnityBuildProfile))
                .HasString("sceneSource", TextVocabulary.GetText(BuildProfileSceneSource.UnityBuildProfile))
                .HasString("buildOptions", "Development"))
            .HasProperty("unityBuildProfile", profile => profile
                .HasString("path", "Assets/BuildProfiles/Linux.asset")
                .HasString("digest", new string('f', 64))
                .HasProperty("applyAudit", applyAudit => applyAudit
                    .HasBoolean("applied", true)
                    .HasProperty("lifecycleBefore", lifecycle => lifecycle
                        .HasProperty("state", state => state
                            .HasProperty("generations", generations => generations
                                .HasInt32("compileGeneration", 20))))
                    .HasProperty("lifecycleAfter", lifecycle => lifecycle
                        .HasProperty("state", state => state
                            .HasProperty("generations", generations => generations
                                .HasInt32("assetRefreshGeneration", 21)
                                .HasInt32("playModeGeneration", 21))))
                    .HasProperty("dirtyStateAfter", dirty => dirty
                        .HasBoolean("dirty", false))));
    }
}
