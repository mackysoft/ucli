using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;

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
        Assert.Equal(1, profile.SchemaVersion);
        Assert.Equal(BuildProfileInputsKind.Explicit, profile.Inputs.Kind);
        Assert.Equal(BuildTargetStableName.StandaloneLinux64, profile.BuildTarget.StableNameValue);
        Assert.Equal("standaloneLinux64", profile.BuildTarget.StableName);
        Assert.Equal("StandaloneLinux64", profile.BuildTarget.UnityBuildTargetLiteral);
        Assert.Equal(BuildProfileSceneSource.Explicit, profile.Scenes.Source);
        Assert.Equal(
            [
                "Assets/Scenes/Main.unity",
                "Assets/Scenes/Bootstrap.unity",
            ],
            profile.Scenes.Paths);
        Assert.False(profile.Options.Development);
        Assert.Equal(BuildProfileRunnerKind.BuildPipeline, profile.Runner.Kind);
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
        Assert.Matches("^[0-9a-f]{64}$", profile.Digest);
        Assert.False(profile.Digest.StartsWith("sha256:", StringComparison.Ordinal));
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
        Assert.Equal(BuildProfileSceneSource.EditorBuildSettings, profile.Scenes.Source);
        Assert.Empty(profile.Scenes.Paths);
        Assert.True(profile.Options.Development);
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
        Assert.Equal(BuildProfileInputsKind.UnityBuildProfile, profile.Inputs.Kind);
        Assert.Equal("Assets/BuildProfiles/Linux.asset", profile.Inputs.RequireUnityBuildProfilePath());
        Assert.Throws<InvalidOperationException>(() => profile.Inputs.RequireBuildTarget());
        Assert.Matches("^[0-9a-f]{64}$", profile.Digest);
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
        var runner = result.Profile!.Runner;
        Assert.Equal(BuildProfileRunnerKind.ExecuteMethod, runner.Kind);
        Assert.Equal("Build.Entry.Run", runner.Method);
        Assert.Equal("${ucli.build.outputDir}", runner.Invocation.Arguments["output"]);
        Assert.Equal("${build.target}", runner.Invocation.Arguments["target"]);
        Assert.Equal(["BUILD_MODE"], runner.Invocation.Environment.Variables);
        Assert.Equal(["UNITY_LICENSE"], runner.Invocation.Environment.Secrets);
    }
}
