using System.Text.Json;

namespace MackySoft.Ucli.Tests.Schemas;

public sealed class ScreenshotPayloadSchemaArtifactTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void ScreenshotPayloadSchemas_AcceptGoldenCaptureContracts ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        using var game = CliOutputGoldenFiles.ReadJsonDocument("screenshot", "game-success.json");
        using var scene = CliOutputGoldenFiles.ReadJsonDocument("screenshot", "scene-success.json");

        Assert.Empty(schemaSet.Validate(
            "cli-output/payload/screenshot.game.schema.json",
            game.RootElement.GetProperty("payload")));
        Assert.Empty(schemaSet.Validate(
            "cli-output/payload/screenshot.scene.schema.json",
            scene.RootElement.GetProperty("payload")));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void ScreenshotPayloadSchemas_RejectCaptureStateOutsideSuccessfulContract ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        using var playing = JsonDocument.Parse(CreateGamePayload(
            artifactPrefix: string.Empty,
            playModeState: "playing"));

        Assert.NotEmpty(schemaSet.Validate(
            "cli-output/payload/screenshot.game.schema.json",
            playing.RootElement));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void ScreenshotPayloadSchemas_RejectRoleInlineImageAndMismatchedSizeModes ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        using var role = JsonDocument.Parse(CreateGamePayload("\"role\": \"evidence\","));
        using var inlineImage = JsonDocument.Parse(CreateGamePayload("\"base64\": \"iVBORw0KGgo=\","));
        using var mismatchedSizeMode = JsonDocument.Parse(CreateGamePayload(
            artifactPrefix: string.Empty,
            sizeMode: "currentSurface"));

        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/screenshot.game.schema.json", role.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/screenshot.game.schema.json", inlineImage.RootElement));
        Assert.NotEmpty(schemaSet.Validate("cli-output/payload/screenshot.game.schema.json", mismatchedSizeMode.RootElement));
    }

    private static string CreateGamePayload (
        string artifactPrefix,
        string sizeMode = "requestedResolution",
        string playModeState = "stopped")
    {
        return $$"""
        {
          "project": {
            "projectPath": "/repo/UnityProject",
            "projectFingerprint": "pf_test",
            "unityVersion": "6000.0.77f1"
          },
          "capture": {
            "target": "game",
            "sizeMode": "{{sizeMode}}",
            "requestedWidth": 1920,
            "requestedHeight": 1080,
            "width": 1920,
            "height": 1080,
            "colorSpace": "linear",
            "lifecycleStateAtCapture": "ready",
            "compileStateAtCapture": "ready",
            "generations": {
              "compileGeneration": 5,
              "domainReloadGeneration": 7,
              "assetRefreshGeneration": 11,
              "playModeGeneration": 13
            },
            "playModeState": "{{playModeState}}"
          },
          "artifact": {
            {{artifactPrefix}}
            "kind": "screenshot",
            "mediaType": "image/png",
            "path": ".ucli/local/projects/<projectStorageKey>/artifacts/screenshot/<captureStorageKey>/screenshot.png",
            "digest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "sizeBytes": 1,
            "createdAtUtc": "2026-07-11T00:00:00+00:00"
          }
        }
        """;
    }
}
