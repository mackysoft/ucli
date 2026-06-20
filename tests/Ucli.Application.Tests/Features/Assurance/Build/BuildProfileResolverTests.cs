using System.Text;
using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildProfileResolverTests
{
    private const string ValidExplicitProfileJson = """
        {
          "schemaVersion": 1,
          "inputs": {
            "kind": "explicit",
            "buildTarget": "standaloneLinux64",
            "scenes": {
              "source": "explicit",
              "paths": [
                "Assets/Scenes/Main.unity",
                "Assets/Scenes/Bootstrap.unity"
              ]
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
                "daemon",
                "oneshot"
              ],
              "allowedEditorModes": [
                "batchmode",
                "gui"
              ]
            },
            "projectMutationMode": "forbid"
          }
        }
        """;

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

    [Theory]
    [MemberData(nameof(InvalidProfileJsonCases))]
    [Trait("Size", "Small")]
    public void ResolveJson_WithInvalidProfileShape_ReturnsBuildProfileInvalid (string json)
    {
        var result = BuildProfileResolver.ResolveJson(json);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Equal(BuildErrorCodes.BuildProfileInvalid, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveJson_WithUnsupportedBuildTarget_ReturnsBuildTargetUnsupported ()
    {
        var result = BuildProfileResolver.ResolveJson(
            """
            {
              "schemaVersion": 1,
              "inputs": {
                "kind": "explicit",
                "buildTarget": "unknownTarget",
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
                    "batchmode"
                  ]
                },
                "projectMutationMode": "forbid"
              }
            }
            """);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Equal(BuildErrorCodes.BuildTargetUnsupported, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveJson_ComputesStableCanonicalDigest ()
    {
        const string CompactJson = "{\"schemaVersion\":1,\"inputs\":{\"kind\":\"explicit\",\"buildTarget\":\"standaloneLinux64\",\"scenes\":{\"source\":\"explicit\",\"paths\":[\"Assets/Scenes/Main.unity\"]},\"options\":{\"development\":false}},\"runner\":{\"kind\":\"buildPipeline\"},\"policy\":{\"runtime\":{\"allowedExecutionModes\":[\"daemon\",\"oneshot\"],\"allowedEditorModes\":[\"batchmode\",\"gui\"]},\"projectMutationMode\":\"forbid\"}}";
        const string ReorderedJson = """
            {
              "policy": {
                "projectMutationMode": "forbid",
                "runtime": {
                  "allowedEditorModes": [
                    "batchmode",
                    "gui"
                  ],
                  "allowedExecutionModes": [
                    "daemon",
                    "oneshot"
                  ]
                }
              },
              "runner": {
                "kind": "buildPipeline"
              },
              "inputs": {
                "options": {
                  "development": false
                },
                "scenes": {
                  "paths": [
                    "Assets/Scenes/Main.unity"
                  ],
                  "source": "explicit"
                },
                "buildTarget": "standaloneLinux64",
                "kind": "explicit"
              },
              "schemaVersion": 1
            }
            """;

        var first = BuildProfileResolver.ResolveJson(CompactJson).Profile!;
        var second = BuildProfileResolver.ResolveJson(ReorderedJson).Profile!;
        var expectedDigest = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(CompactJson));

        Assert.Equal(first.Digest, second.Digest);
        Assert.Equal(expectedDigest, first.Digest);
        Assert.Matches("^[0-9a-f]{64}$", first.Digest);
        Assert.False(first.Digest.StartsWith("sha256:", StringComparison.Ordinal));
    }

    public static TheoryData<string> InvalidProfileJsonCases ()
    {
        return
        [
            """not-json""",
            """ """,
            """[]""",
            """{"schemaVersion":1,"buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"output":{"kind":"legacyArtifact"},"options":{"development":false}}""",
            """{"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":2,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"},"legacy":true}""",
            """{"schemaVersion":1,"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"}}""",
            """{"schemaVersion":1,"inputs":[],"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false},"legacy":true},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"unityBuildProfile","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":" standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            "{\"schemaVersion\":1,\"inputs\":{\"kind\":\"explicit\",\"buildTarget\":\"standaloneLinux64\\n\",\"scenes\":{\"source\":\"editorBuildSettings\"},\"options\":{\"development\":false}},\"runner\":{\"kind\":\"buildPipeline\"},\"policy\":{\"runtime\":{\"allowedExecutionModes\":[\"daemon\"],\"allowedEditorModes\":[\"batchmode\"]},\"projectMutationMode\":\"forbid\"}}",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":[],"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"unknown"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings","paths":["Assets/Scenes/Main.unity"]},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"explicit"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"explicit","paths":[]},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"explicit","paths":[" "]},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"explicit","paths":["Assets/Scenes/Main.unity","Assets/Scenes/Main.unity"]},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"explicit","paths":["/Assets/Scenes/Main.unity"]},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"explicit","paths":["Assets/Scenes/../Main.unity"]},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"explicit","paths":["Assets\\Scenes\\Main.unity"]},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"explicit","paths":["Assets/Scenes/Main.scene"]},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":[]},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false,"legacy":true}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false,"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":"false"}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline","kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"executeMethod"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline","method":"Build.Run"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":[]}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]}}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid","legacy":true}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid","projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":[],"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"],"legacy":true},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":[],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":[true],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["auto"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon","daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":[]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":[true]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["headless"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode","batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"legacy"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"explicit","buildTarget":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"options":{"development":false}},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":" forbid"}}""",
        ];
    }
}
