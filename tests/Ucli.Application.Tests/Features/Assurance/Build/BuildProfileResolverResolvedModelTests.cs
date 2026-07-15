using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildProfileResolverTestSupport;

public sealed class BuildProfileResolverResolvedModelTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ResolveJson_WithFinalExplicitBuildPipelineProfile_ReturnsResolvedModel ()
    {
        var result = BuildProfileResolver.ResolveJson(ValidExplicitProfileJson);

        Assert.True(result.IsSuccess);
        var profile = result.Profile!;
        var inputs = Assert.IsType<ResolvedBuildInputs.Explicit>(profile.Inputs);
        var scenes = Assert.IsType<ResolvedBuildScenes.Explicit>(inputs.Scenes);
        Assert.Equal(1, profile.SchemaVersion);
        Assert.Equal(BuildProfileInputsKind.Explicit, profile.Inputs.Kind);
        Assert.Equal(BuildTargetStableName.StandaloneLinux64, inputs.BuildTarget);
        Assert.Equal(BuildProfileSceneSource.Explicit, scenes.Source);
        Assert.Equal(
            [
                new SceneAssetPath("Assets/Scenes/Main.unity"),
                new SceneAssetPath("Assets/Scenes/Bootstrap.unity"),
            ],
            scenes.Paths);
        Assert.False(inputs.Options.Development);
        Assert.IsType<ResolvedBuildRunner.BuildPipeline>(profile.Runner);
        Assert.Equal(
            [
                BuildProfileRuntimeExecutionMode.Daemon,
                BuildProfileRuntimeExecutionMode.Oneshot,
            ],
            profile.Policy.Runtime.AllowedExecutionModes);
        Assert.Equal(
            [
                DaemonEditorMode.Batchmode,
                DaemonEditorMode.Gui,
            ],
            profile.Policy.Runtime.AllowedEditorModes);
        Assert.Equal(BuildProfileProjectMutationMode.Forbid, profile.Policy.ProjectMutationMode);
        Assert.Matches("^[0-9a-f]{64}$", profile.Digest.ToString());
        Assert.False(profile.Digest.ToString().StartsWith("sha256:", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveJson_WithPortableSceneSeparators_NormalizesAtProfileBoundary ()
    {
        var json = ValidExplicitProfileJson.Replace(
            "Assets/Scenes/Main.unity",
            "Assets\\\\Scenes\\\\Main.unity",
            StringComparison.Ordinal);

        var result = BuildProfileResolver.ResolveJson(json);

        Assert.True(result.IsSuccess);
        var inputs = Assert.IsType<ResolvedBuildInputs.Explicit>(result.Profile!.Inputs);
        var scenes = Assert.IsType<ResolvedBuildScenes.Explicit>(inputs.Scenes);
        Assert.Equal(
            new SceneAssetPath("Assets/Scenes/Main.unity"),
            scenes.Paths[0]);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveJson_WithEditorBuildSettingsScenes_PreservesSourceWithoutScenePaths ()
    {
        var result = BuildProfileResolver.ResolveJson(
            """
            {
              "schemaVersion": 1,
              "inputs": {
                "kind": "explicit",
                "buildTarget": "standaloneLinux64",
                "scenes": {
                  "source": "editorBuildSettings"
                },
                "options": {
                  "development": true
                }
              },
              "runner": {
                "kind": "buildPipeline"
              },
              "policy": {
                "runtime": {
                  "allowedExecutionModes": [
                    "daemon"
                  ],
                  "allowedEditorModes": [
                    "batchmode"
                  ]
                },
                "projectMutationMode": "audit"
              }
            }
            """);

        Assert.True(result.IsSuccess);
        var profile = result.Profile!;
        var inputs = Assert.IsType<ResolvedBuildInputs.Explicit>(profile.Inputs);
        var scenes = Assert.IsType<ResolvedBuildScenes.EditorBuildSettings>(inputs.Scenes);
        Assert.Equal(BuildProfileSceneSource.EditorBuildSettings, scenes.Source);
        Assert.True(inputs.Options.Development);
        Assert.Equal([BuildProfileRuntimeExecutionMode.Daemon], profile.Policy.Runtime.AllowedExecutionModes);
        Assert.Equal([DaemonEditorMode.Batchmode], profile.Policy.Runtime.AllowedEditorModes);
        Assert.Equal(BuildProfileProjectMutationMode.Audit, profile.Policy.ProjectMutationMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveJson_WithAllowWithAuditProjectMutationMode_ReturnsResolvedPolicy ()
    {
        var result = BuildProfileResolver.ResolveJson(
            """
            {
              "schemaVersion": 1,
              "inputs": {
                "kind": "explicit",
                "buildTarget": "standaloneLinux64",
                "scenes": {
                  "source": "editorBuildSettings"
                },
                "options": {
                  "development": false
                }
              },
              "runner": {
                "kind": "buildPipeline"
              },
              "policy": {
                "runtime": {
                  "allowedExecutionModes": [
                    "oneshot"
                  ],
                  "allowedEditorModes": [
                    "gui"
                  ]
                },
                "projectMutationMode": "allowWithAudit"
              }
            }
            """);

        Assert.True(result.IsSuccess);
        var profile = result.Profile!;
        Assert.Equal(BuildProfileProjectMutationMode.AllowWithAudit, profile.Policy.ProjectMutationMode);
        Assert.Equal([BuildProfileRuntimeExecutionMode.Oneshot], profile.Policy.Runtime.AllowedExecutionModes);
        Assert.Equal([DaemonEditorMode.Gui], profile.Policy.Runtime.AllowedEditorModes);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveJson_WithUnityBuildProfileInput_ReturnsPathOnlyResolvedInput ()
    {
        var result = BuildProfileResolver.ResolveJson(
            """
            {
              "schemaVersion": 1,
              "inputs": {
                "kind": "unityBuildProfile",
                "path": "Assets/BuildProfiles/Linux.asset"
              },
              "runner": {
                "kind": "buildPipeline"
              },
              "policy": {
                "runtime": {
                  "allowedExecutionModes": [
                    "daemon"
                  ],
                  "allowedEditorModes": [
                    "batchmode"
                  ]
                },
                "projectMutationMode": "forbid"
              }
            }
            """);

        Assert.True(result.IsSuccess);
        var profile = result.Profile!;
        var inputs = Assert.IsType<ResolvedBuildInputs.UnityBuildProfile>(profile.Inputs);
        Assert.Equal(BuildProfileInputsKind.UnityBuildProfile, profile.Inputs.Kind);
        Assert.Equal("Assets/BuildProfiles/Linux.asset", inputs.Path.Value);
        Assert.Matches("^[0-9a-f]{64}$", profile.Digest.ToString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveJson_WithExecuteMethodRunner_ReturnsInvocationModel ()
    {
        var result = BuildProfileResolver.ResolveJson(CreateProfileJson(
            """
            {
              "kind": "executeMethod",
              "method": "Build.Entry.Run",
              "invocation": {
                "arguments": {
                  "output": "${ucli.build.outputDir}",
                  "target": "${build.target}"
                },
                "environment": {
                  "variables": [
                    "BUILD_MODE"
                  ],
                  "secrets": [
                    "UNITY_LICENSE"
                  ]
                }
              }
            }
            """));

        Assert.True(result.IsSuccess);
        var runner = Assert.IsType<ResolvedBuildRunner.ExecuteMethod>(result.Profile!.Runner);
        Assert.Equal(BuildRunnerKind.ExecuteMethod, runner.Kind);
        Assert.Equal("Build.Entry.Run", runner.Method);
        Assert.Equal("${ucli.build.outputDir}", runner.Invocation.Arguments["output"]);
        Assert.Equal("${build.target}", runner.Invocation.Arguments["target"]);
        Assert.Equal(["BUILD_MODE"], runner.Invocation.Environment.Variables);
        Assert.Equal(["UNITY_LICENSE"], runner.Invocation.Environment.Secrets);
    }
}
