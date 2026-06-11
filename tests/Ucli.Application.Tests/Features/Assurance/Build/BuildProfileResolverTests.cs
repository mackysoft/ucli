using System.Security.Cryptography;
using System.Text;
using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildProfileResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ResolveJson_WithExplicitScenesProfile_ReturnsResolvedModel ()
    {
        var result = BuildProfileResolver.ResolveJson(
            """
            {
              "schemaVersion": 1,
              "target": "standaloneLinux64",
              "scenes": {
                "source": "explicit",
                "paths": [
                  "Assets/Scenes/Main.unity",
                  "Assets/Scenes/Bootstrap.unity"
                ]
              },
              "output": {
                "kind": "ucliArtifact"
              },
              "options": {
                "development": false
              }
            }
            """);

        Assert.True(result.IsSuccess);
        var profile = result.Profile!;
        Assert.Equal(1, profile.SchemaVersion);
        Assert.Equal("standaloneLinux64", profile.Target.StableName);
        Assert.Equal("StandaloneLinux64", profile.Target.UnityBuildTargetLiteral);
        Assert.Equal("explicit", profile.Scenes.Source);
        Assert.Equal(
            [
                "Assets/Scenes/Main.unity",
                "Assets/Scenes/Bootstrap.unity",
            ],
            profile.Scenes.Paths);
        Assert.Equal("ucliArtifact", profile.Output.Kind);
        Assert.False(profile.Options.Development);
        Assert.Matches("^[0-9a-f]{64}$", profile.Digest);
        Assert.False(profile.Digest.StartsWith("sha256:", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveJson_WithEditorBuildSettingsProfile_PreservesSourceWithoutScenePaths ()
    {
        var result = BuildProfileResolver.ResolveJson(
            """
            {
              "schemaVersion": 1,
              "target": "standaloneLinux64",
              "scenes": {
                "source": "editorBuildSettings"
              },
              "output": {
                "kind": "ucliArtifact"
              },
              "options": {
                "development": true
              }
            }
            """);

        Assert.True(result.IsSuccess);
        var profile = result.Profile!;
        Assert.Equal("editorBuildSettings", profile.Scenes.Source);
        Assert.Empty(profile.Scenes.Paths);
        Assert.True(profile.Options.Development);
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
    public void ResolveJson_WithUnsupportedTarget_ReturnsBuildTargetUnsupported ()
    {
        var result = BuildProfileResolver.ResolveJson(
            """
            {
              "schemaVersion": 1,
              "target": "standaloneWindows64",
              "scenes": {
                "source": "editorBuildSettings"
              },
              "output": {
                "kind": "ucliArtifact"
              },
              "options": {
                "development": false
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
        const string CompactJson = "{\"schemaVersion\":1,\"target\":\"standaloneLinux64\",\"scenes\":{\"source\":\"explicit\",\"paths\":[\"Assets/Scenes/Main.unity\"]},\"output\":{\"kind\":\"ucliArtifact\"},\"options\":{\"development\":false}}";
        const string ReorderedJson = """
            {
              "options": {
                "development": false
              },
              "output": {
                "kind": "ucliArtifact"
              },
              "scenes": {
                "paths": [
                  "Assets/Scenes/Main.unity"
                ],
                "source": "explicit"
              },
              "target": "standaloneLinux64",
              "schemaVersion": 1
            }
            """;

        var first = BuildProfileResolver.ResolveJson(CompactJson).Profile!;
        var second = BuildProfileResolver.ResolveJson(ReorderedJson).Profile!;
        var expectedDigest = ComputeSha256LowerHex(CompactJson);

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
            """{"target":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"output":{"kind":"ucliArtifact"},"options":{"development":false}}""",
            """{"schemaVersion":2,"target":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"output":{"kind":"ucliArtifact"},"options":{"development":false}}""",
            """{"schemaVersion":1,"target":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"output":{"kind":"ucliArtifact"},"options":{"development":false},"legacy":true}""",
            """{"schemaVersion":1,"target":"","scenes":{"source":"editorBuildSettings"},"output":{"kind":"ucliArtifact"},"options":{"development":false}}""",
            """{"schemaVersion":1,"target":"standaloneLinux64","scenes":{"source":"unknown"},"output":{"kind":"ucliArtifact"},"options":{"development":false}}""",
            """{"schemaVersion":1,"target":"standaloneLinux64","scenes":{"source":"editorBuildSettings","paths":["Assets/Scenes/Main.unity"]},"output":{"kind":"ucliArtifact"},"options":{"development":false}}""",
            """{"schemaVersion":1,"target":"standaloneLinux64","scenes":{"source":"explicit"},"output":{"kind":"ucliArtifact"},"options":{"development":false}}""",
            """{"schemaVersion":1,"target":"standaloneLinux64","scenes":{"source":"explicit","paths":[]},"output":{"kind":"ucliArtifact"},"options":{"development":false}}""",
            """{"schemaVersion":1,"target":"standaloneLinux64","scenes":{"source":"explicit","paths":[" "]},"output":{"kind":"ucliArtifact"},"options":{"development":false}}""",
            """{"schemaVersion":1,"target":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"output":{"kind":"fileSystem"},"options":{"development":false}}""",
            """{"schemaVersion":1,"target":"standaloneLinux64","scenes":{"source":"editorBuildSettings"},"output":{"kind":"ucliArtifact"},"options":{"development":"false"}}""",
        ];
    }

    private static string ComputeSha256LowerHex (string utf8Text)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(utf8Text));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
