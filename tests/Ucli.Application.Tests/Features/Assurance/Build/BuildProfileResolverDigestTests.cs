using System.Text;
using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildProfileResolverTestSupport;

public sealed class BuildProfileResolverDigestTests
{
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

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveJson_ComputesExecuteMethodDigestWithCanonicalInvocationArgumentOrder ()
    {
        var first = BuildProfileResolver.ResolveJson(CreateProfileJson(
            """
            {
              "kind": "executeMethod",
              "method": "Build.Entry.Run",
              "invocation": {
                "arguments": {
                  "b": "2",
                  "a": "1"
                },
                "environment": {
                  "variables": [
                    "BUILD_MODE"
                  ],
                  "secrets": [
                    "UNITY_LICENSE",
                    "UNITY_EMAIL"
                  ]
                }
              }
            }
            """)).Profile!;
        var second = BuildProfileResolver.ResolveJson(CreateProfileJson(
            """
            {
              "kind": "executeMethod",
              "method": "Build.Entry.Run",
              "invocation": {
                "environment": {
                  "variables": [
                    "BUILD_MODE"
                  ],
                  "secrets": [
                    "UNITY_LICENSE",
                    "UNITY_EMAIL"
                  ]
                },
                "arguments": {
                  "a": "1",
                  "b": "2"
                }
              }
            }
            """)).Profile!;

        Assert.Equal(first.Digest, second.Digest);
        Assert.NotEqual(BuildProfileResolver.ResolveJson(ValidExplicitProfileJson).Profile!.Digest, first.Digest);
    }
}
