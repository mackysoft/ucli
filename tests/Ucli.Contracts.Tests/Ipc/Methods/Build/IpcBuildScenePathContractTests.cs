using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Methods.Build;

public sealed class IpcBuildScenePathContractTests
{
    private static readonly Guid RunId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Sha256Digest ProfileDigest = Sha256Digest.Parse(new string('a', 64));

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildRunRequest_WhenScenePathsIsNull_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => CreateRequest(null!, [DaemonEditorMode.Batchmode]));

        Assert.Equal("ScenePaths", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildRunRequest_WhenScenePathsContainsNull_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateRequest([null!], [DaemonEditorMode.Batchmode]));

        Assert.Equal("ScenePaths", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildRunRequest_WhenScenePathSourceChanges_PreservesTypedSnapshot ()
    {
        var expected = new SceneAssetPath("Assets/Scenes/Main.unity");
        var source = new[] { expected };
        var request = CreateRequest(source, [DaemonEditorMode.Batchmode]);

        source[0] = new SceneAssetPath("Assets/Scenes/Other.unity");

        Assert.Equal(expected, Assert.Single(request.ScenePaths));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildRunRequest_WhenAllowedEditorModesContainsUndefinedValue_ThrowsArgumentOutOfRangeException ()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateRequest(
            [new SceneAssetPath("Assets/Scenes/Main.unity")],
            [default]));

        Assert.Equal("AllowedEditorModes", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildRunRequest_WhenAllowedEditorModesIsNull_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => CreateRequest(
            [new SceneAssetPath("Assets/Scenes/Main.unity")],
            null!));

        Assert.Equal("AllowedEditorModes", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildRunRequest_WhenAllowedEditorModeSourceChanges_PreservesSnapshot ()
    {
        var source = new[] { DaemonEditorMode.Batchmode };
        var request = CreateRequest(
            [new SceneAssetPath("Assets/Scenes/Main.unity")],
            source);

        source[0] = DaemonEditorMode.Gui;

        Assert.Equal(DaemonEditorMode.Batchmode, Assert.Single(request.AllowedEditorModes));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildInputProbe_WhenScenesIsNull_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => CreateInputProbe(null!));

        Assert.Equal("Scenes", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildInputProbe_WhenScenesContainsNull_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateInputProbe([null!]));

        Assert.Equal("Scenes", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildInputProbe_WhenSceneSourceChanges_PreservesTypedSnapshot ()
    {
        var expected = new SceneAssetPath("Assets/Scenes/Main.unity");
        var source = new[] { expected };
        var probe = CreateInputProbe(source);

        source[0] = new SceneAssetPath("Assets/Scenes/Other.unity");

        Assert.Equal(expected, Assert.Single(probe.Scenes));
    }

    private static IpcBuildRunRequest CreateRequest (
        IReadOnlyList<SceneAssetPath> scenePaths,
        IReadOnlyList<DaemonEditorMode> allowedEditorModes)
    {
        return new IpcBuildRunRequest(
            RunId: RunId,
            InputKind: BuildProfileInputsKind.Explicit,
            BuildTarget: BuildTargetStableName.StandaloneLinux64,
            SceneSource: BuildProfileSceneSource.Explicit,
            ScenePaths: scenePaths,
            Development: false,
            OutputPath: "/tmp/ucli/output",
            OutputLayout: new IpcBuildOutputLayout(
                IpcBuildOutputLayoutShape.File,
                "/tmp/ucli/output/Player"),
            BuildReportPath: "/tmp/ucli/build-report.json",
            BuildLogPath: "/tmp/ucli/build.log",
            AllowedEditorModes: allowedEditorModes,
            ProjectMutationMode: BuildProfileProjectMutationMode.Forbid,
            RunnerKind: BuildRunnerKind.BuildPipeline,
            ProfileDigest: ProfileDigest,
            UnityBuildProfile: null,
            ProfilePath: null,
            RunnerMethod: null,
            RunnerArguments: new Dictionary<string, string>(),
            RunnerEnvironmentVariables: [],
            RunnerEnvironmentSecrets: [],
            RunnerEnvironmentVariableValues: new Dictionary<string, string>(),
            RunnerEnvironmentSecretValues: new Dictionary<string, string>());
    }

    private static IpcBuildInputProbe CreateInputProbe (IReadOnlyList<SceneAssetPath> scenes)
    {
        return new IpcBuildInputProbe(
            InputKind: BuildProfileInputsKind.Explicit,
            BuildTarget: BuildTargetStableName.StandaloneLinux64,
            UnityBuildTarget: "StandaloneLinux64",
            UnityBuildTargetGroup: "Standalone",
            SceneSource: BuildProfileSceneSource.Explicit,
            Scenes: scenes,
            BuildOptions: "None");
    }
}
