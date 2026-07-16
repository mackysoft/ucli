using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class ResolvedBuildProfileContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ExplicitInputs_WithUndefinedBuildTarget_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResolvedBuildInputs.Explicit(
            (BuildTargetStableName)int.MaxValue,
            new ResolvedBuildScenes.EditorBuildSettings(),
            new ResolvedBuildOptions(Development: false)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ExplicitInputs_WithNullScenes_Throws ()
    {
        Assert.Throws<ArgumentNullException>(() => new ResolvedBuildInputs.Explicit(
            BuildTargetStableName.StandaloneLinux64,
            null!,
            new ResolvedBuildOptions(Development: false)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ExplicitScenes_WithNullPaths_Throws ()
    {
        Assert.Throws<ArgumentNullException>(() => new ResolvedBuildScenes.Explicit(null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ExplicitScenes_WithDuplicatePaths_Throws ()
    {
        var path = new SceneAssetPath("Assets/Scenes/Main.unity");

        Assert.Throws<ArgumentException>(() => new ResolvedBuildScenes.Explicit([path, path]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ExplicitScenes_CopiesPaths ()
    {
        var paths = new List<SceneAssetPath>
        {
            new("Assets/Scenes/Main.unity"),
        };
        var scenes = new ResolvedBuildScenes.Explicit(paths);

        paths[0] = new SceneAssetPath("Assets/Scenes/Other.unity");

        Assert.Equal(new SceneAssetPath("Assets/Scenes/Main.unity"), scenes.Paths[0]);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ExecuteMethodRunner_WithInvalidMethod_Throws ()
    {
        Assert.Throws<ArgumentException>(() => new ResolvedBuildRunner.ExecuteMethod(
            "BuildEntry",
            ResolvedBuildRunnerInvocation.Empty));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ExecuteMethodRunner_WithNullInvocation_Throws ()
    {
        Assert.Throws<ArgumentNullException>(() => new ResolvedBuildRunner.ExecuteMethod(
            "Build.Entry.Run",
            null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RunnerInvocation_CopiesArgumentsAndEnvironmentNames ()
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["target"] = "standaloneLinux64",
        };
        var variables = new List<string> { "BUILD_MODE" };
        var invocation = new ResolvedBuildRunnerInvocation(
            arguments,
            new ResolvedBuildRunnerEnvironment(variables, Array.Empty<string>()));

        arguments["target"] = "android";
        variables[0] = "OTHER_MODE";

        Assert.Equal("standaloneLinux64", invocation.Arguments["target"]);
        Assert.Equal(["BUILD_MODE"], invocation.Environment.Variables);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RuntimePolicy_WithUndefinedExecutionMode_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResolvedBuildRuntimePolicy(
            [(BuildProfileRuntimeExecutionMode)int.MaxValue],
            [DaemonEditorMode.Batchmode]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RuntimePolicy_WithNullExecutionModes_Throws ()
    {
        Assert.Throws<ArgumentNullException>(() => new ResolvedBuildRuntimePolicy(
            null!,
            [DaemonEditorMode.Batchmode]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Policy_WithUndefinedProjectMutationMode_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResolvedBuildPolicy(
            CreateRuntimePolicy(),
            (BuildProfileProjectMutationMode)int.MaxValue));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Policy_WithNullRuntime_Throws ()
    {
        Assert.Throws<ArgumentNullException>(() => new ResolvedBuildPolicy(
            null!,
            BuildProfileProjectMutationMode.Forbid));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Profile_WithUnsupportedSchemaVersion_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResolvedBuildProfile(
            int.MaxValue,
            new ResolvedBuildInputs.Explicit(
                BuildTargetStableName.StandaloneLinux64,
                new ResolvedBuildScenes.EditorBuildSettings(),
                new ResolvedBuildOptions(Development: false)),
            new ResolvedBuildRunner.BuildPipeline(),
            CreatePolicy()));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Profile_WithNullInputs_Throws ()
    {
        Assert.Throws<ArgumentNullException>(() => new ResolvedBuildProfile(
            ResolvedBuildProfile.SupportedSchemaVersion,
            null!,
            new ResolvedBuildRunner.BuildPipeline(),
            CreatePolicy()));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Profile_WithUnityBuildProfileInputsAndExecuteMethodRunner_Throws ()
    {
        var inputs = new ResolvedBuildInputs.UnityBuildProfile(
            new UnityBuildProfileAssetPath("Assets/BuildProfiles/Linux.asset"));
        var runner = new ResolvedBuildRunner.ExecuteMethod(
            "Build.Entry.Run",
            ResolvedBuildRunnerInvocation.Empty);

        Assert.Throws<ArgumentException>(() => new ResolvedBuildProfile(
            ResolvedBuildProfile.SupportedSchemaVersion,
            inputs,
            runner,
            CreatePolicy()));
    }

    private static ResolvedBuildPolicy CreatePolicy ()
    {
        return new ResolvedBuildPolicy(
            CreateRuntimePolicy(),
            BuildProfileProjectMutationMode.Forbid);
    }

    private static ResolvedBuildRuntimePolicy CreateRuntimePolicy ()
    {
        return new ResolvedBuildRuntimePolicy(
            [BuildProfileRuntimeExecutionMode.Daemon],
            [DaemonEditorMode.Batchmode]);
    }
}
