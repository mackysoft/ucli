using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildProfileResolverTestSupport;

public sealed class BuildProfileResolverValidationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ResolveJson_WithInvalidExecuteMethodRunner_ReturnsBuildProfileInvalid ()
    {
        foreach (string runnerJson in InvalidExecuteMethodRunnerJsonCases())
        {
            var result = BuildProfileResolver.ResolveJson(CreateProfileJson(runnerJson));

            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
            Assert.Equal(BuildErrorCodes.BuildProfileInvalid, result.Error.Code);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveJson_WithInvalidProfileShape_ReturnsBuildProfileInvalid ()
    {
        foreach (string json in InvalidProfileJsonCases())
        {
            var result = BuildProfileResolver.ResolveJson(json);

            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
            Assert.Equal(BuildErrorCodes.BuildProfileInvalid, result.Error.Code);
        }
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

    private static string[] InvalidExecuteMethodRunnerJsonCases ()
    {
        return
        [
            """{"kind":"executeMethod","method":"Run"}""",
            """{"kind":"executeMethod","method":"Build.Entry.Run, Assembly"}""",
            """{"kind":"executeMethod","method":"Build.Entry.Run","legacy":true}""",
            """{"kind":"executeMethod","method":"Build.Entry.Run","invocation":[]}""",
            """{"kind":"executeMethod","method":"Build.Entry.Run","invocation":{"arguments":[]}}""",
            """{"kind":"executeMethod","method":"Build.Entry.Run","invocation":{"arguments":{"output":1}}}""",
            """{"kind":"executeMethod","method":"Build.Entry.Run","invocation":{"environment":[]}}""",
            """{"kind":"executeMethod","method":"Build.Entry.Run","invocation":{"environment":{"variables":[1]}}}""",
            """{"kind":"executeMethod","method":"Build.Entry.Run","invocation":{"environment":{"variables":["BUILD_MODE","BUILD_MODE"]}}}""",
            """{"kind":"executeMethod","method":"Build.Entry.Run","invocation":{"environment":{"variables":["BUILD_MODE"],"secrets":["BUILD_MODE"]}}}""",
            """{"kind":"executeMethod","method":"Build.Entry.Run","invocation":{"environment":{"legacy":[]}}}""",
            """{"kind":"executeMethod","method":"Build.Entry.Run","invocation":{"arguments":{},"environment":{},"legacy":true}}""",
        ];
    }

    private static string[] InvalidProfileJsonCases ()
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
            """{"schemaVersion":1,"inputs":{"kind":"unityBuildProfile"},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"unityBuildProfile","path":"/Assets/BuildProfiles/Linux.asset"},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"unityBuildProfile","path":"Assets/../BuildProfiles/Linux.asset"},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"unityBuildProfile","path":"Packages/BuildProfiles/Linux.asset"},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"unityBuildProfile","path":"Assets/BuildProfiles/Linux.asset.meta"},"runner":{"kind":"buildPipeline"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
            """{"schemaVersion":1,"inputs":{"kind":"unityBuildProfile","path":"Assets/BuildProfiles/Linux.asset"},"runner":{"kind":"executeMethod","method":"Build.Entry.Run"},"policy":{"runtime":{"allowedExecutionModes":["daemon"],"allowedEditorModes":["batchmode"]},"projectMutationMode":"forbid"}}""",
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
