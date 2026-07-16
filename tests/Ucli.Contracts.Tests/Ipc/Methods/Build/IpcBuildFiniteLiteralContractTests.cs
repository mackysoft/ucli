using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Methods.Build;

public sealed class IpcBuildFiniteLiteralContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildDirtyState_WhenCoverageIsUndefined_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IpcBuildDirtyState(
                Dirty: false,
                Coverage: (IpcBuildDirtyStateCoverage)0,
                Items: []));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildDirtyStateItem_WhenKindIsUndefined_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IpcBuildDirtyStateItem(
                Kind: (IpcBuildDirtyStateItemKind)0,
                Path: new ProjectMutationAuditPath("Assets/Scenes/Main.unity")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildInputProbe_WhenInputKindIsUndefined_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateInputProbe(
                inputKind: (BuildProfileInputsKind)0,
                sceneSource: BuildProfileSceneSource.Explicit));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildInputProbe_WhenSceneSourceIsUndefined_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateInputProbe(
                inputKind: BuildProfileInputsKind.Explicit,
                sceneSource: (BuildProfileSceneSource)0));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildOutputLayout_WhenShapeIsUndefined_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IpcBuildOutputLayout(
                Shape: (IpcBuildOutputLayoutShape)0,
                LocationPathName: "/tmp/ucli/output/player"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildReportArtifact_WhenResultIsUndefined_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IpcBuildReportArtifact(
                SchemaVersion: 1,
                Result: (IpcBuildReportResult)0,
                UnityBuildTarget: "StandaloneLinux64",
                OutputPath: "/tmp/ucli/output/player",
                DurationMilliseconds: 0,
                TotalSizeBytes: 0,
                ErrorCount: 0,
                WarningCount: 0,
                Steps: [],
                Messages: []));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildLogSummary_WhenCompletionReasonIsUndefined_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IpcBuildLogSummary(
                EntryCount: 0,
                ErrorCount: 0,
                WarningCount: 0,
                CompletionReason: (IpcBuildLogCompletionReason)0,
                Window: new IpcBuildLogWindow(
                    DateTimeOffset.UnixEpoch,
                    DateTimeOffset.UnixEpoch,
                    CursorStart: null,
                    CursorEnd: null)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildRunnerResultArtifact_WhenSourceIsUndefined_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateRunnerResult(
                source: (IpcBuildRunnerResultSource)0,
                status: IpcBuildReportResult.Succeeded));
    }

    [Theory]
    [InlineData((IpcBuildReportResult)0)]
    [InlineData(IpcBuildReportResult.Unknown)]
    [InlineData((IpcBuildReportResult)999)]
    [Trait("Size", "Small")]
    public void IpcBuildRunnerResultArtifact_WhenStatusIsNotTerminal_Throws (IpcBuildReportResult status)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateRunnerResult(
                source: IpcBuildRunnerResultSource.UcliBuildRunnerResult,
                status: status));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildRunRequest_WhenSceneSourceIsUndefined_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IpcBuildRunRequest(
                RunId: Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                InputKind: BuildProfileInputsKind.Explicit,
                BuildTarget: BuildTargetStableName.StandaloneLinux64,
                SceneSource: (BuildProfileSceneSource)0,
                ScenePaths: [new SceneAssetPath("Assets/Scenes/Main.unity")],
                Development: false,
                OutputPath: "/tmp/ucli/output",
                OutputLayout: null,
                BuildReportPath: "/tmp/ucli/build-report.json",
                BuildLogPath: "/tmp/ucli/build.log",
                AllowedEditorModes: [DaemonEditorMode.Batchmode],
                ProjectMutationMode: BuildProfileProjectMutationMode.Forbid,
                RunnerKind: BuildRunnerKind.BuildPipeline,
                ProfileDigest: Sha256Digest.Parse(new string('a', 64)),
                UnityBuildProfile: null,
                ProfilePath: null,
                RunnerMethod: null,
                RunnerArguments: new Dictionary<string, string>(),
                RunnerEnvironmentVariables: [],
                RunnerEnvironmentSecrets: [],
                RunnerEnvironmentVariableValues: new Dictionary<string, string>(),
                RunnerEnvironmentSecretValues: new Dictionary<string, string>()));
    }

    private static IpcBuildInputProbe CreateInputProbe (
        BuildProfileInputsKind inputKind,
        BuildProfileSceneSource sceneSource)
    {
        return new IpcBuildInputProbe(
            InputKind: inputKind,
            BuildTarget: BuildTargetStableName.StandaloneLinux64,
            UnityBuildTarget: "StandaloneLinux64",
            UnityBuildTargetGroup: "Standalone",
            SceneSource: sceneSource,
            Scenes: [new SceneAssetPath("Assets/Scenes/Main.unity")],
            BuildOptions: "None");
    }

    private static IpcBuildRunnerResultArtifact CreateRunnerResult (
        IpcBuildRunnerResultSource source,
        IpcBuildReportResult status)
    {
        return new IpcBuildRunnerResultArtifact(
            Source: source,
            Status: status,
            DurationMilliseconds: 0,
            ErrorCount: 0,
            WarningCount: 0,
            Diagnostics: [],
            Outputs: [new BuildRunnerOutputPath("player.txt")],
            BuildReport: null);
    }
}
