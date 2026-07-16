using System.Text.Json;
using System.Text.Json.Nodes;

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

    [Theory]
    [InlineData("currentSurface", false, "ready", "stopped")]
    [InlineData("currentSurface", false, "playmode", "playing")]
    [InlineData("requestedResolution", true, "ready", "stopped")]
    [InlineData("requestedResolution", true, "playmode", "playing")]
    [Trait("Size", "Medium")]
    public void ScreenshotGamePayloadSchema_AcceptsSupportedSizeAndCaptureStateVariants (
        string sizeMode,
        bool hasRequestedResolution,
        string lifecycleStateAtCapture,
        string playModeState)
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        using var payload = JsonDocument.Parse(CreateGamePayload(
            artifactPrefix: string.Empty,
            sizeMode: sizeMode,
            hasRequestedResolution: hasRequestedResolution,
            lifecycleStateAtCapture: lifecycleStateAtCapture,
            playModeState: playModeState));

        Assert.Empty(schemaSet.Validate(
            "cli-output/payload/screenshot.game.schema.json",
            payload.RootElement));
    }

    [Theory]
    [InlineData("ready", "playing")]
    [InlineData("playmode", "stopped")]
    [Trait("Size", "Medium")]
    public void ScreenshotGamePayloadSchema_RejectsIncoherentCaptureStatePair (
        string lifecycleStateAtCapture,
        string playModeState)
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        using var payload = JsonDocument.Parse(CreateGamePayload(
            artifactPrefix: string.Empty,
            lifecycleStateAtCapture: lifecycleStateAtCapture,
            playModeState: playModeState));

        Assert.NotEmpty(schemaSet.Validate(
            "cli-output/payload/screenshot.game.schema.json",
            payload.RootElement));
    }

    [Theory]
    [InlineData("ready", "stopped")]
    [InlineData("playmode", "playing")]
    [Trait("Size", "Medium")]
    public void ScreenshotScenePayloadSchema_AcceptsSupportedCaptureStateVariants (
        string lifecycleStateAtCapture,
        string playModeState)
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        using var golden = CliOutputGoldenFiles.ReadJsonDocument("screenshot", "scene-success.json");
        var payloadNode = JsonNode.Parse(
            golden.RootElement.GetProperty("payload").GetRawText())!.AsObject();
        var captureNode = payloadNode["capture"]!.AsObject();
        captureNode["lifecycleStateAtCapture"] = lifecycleStateAtCapture;
        captureNode["playModeState"] = playModeState;
        using var payload = JsonDocument.Parse(payloadNode.ToJsonString());

        Assert.Empty(schemaSet.Validate(
            "cli-output/payload/screenshot.scene.schema.json",
            payload.RootElement));
    }

    [Theory]
    [InlineData("ready", "playing")]
    [InlineData("playmode", "stopped")]
    [Trait("Size", "Medium")]
    public void ScreenshotScenePayloadSchema_RejectsIncoherentCaptureStatePair (
        string lifecycleStateAtCapture,
        string playModeState)
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        using var golden = CliOutputGoldenFiles.ReadJsonDocument("screenshot", "scene-success.json");
        var payloadNode = JsonNode.Parse(
            golden.RootElement.GetProperty("payload").GetRawText())!.AsObject();
        var captureNode = payloadNode["capture"]!.AsObject();
        captureNode["lifecycleStateAtCapture"] = lifecycleStateAtCapture;
        captureNode["playModeState"] = playModeState;
        using var payload = JsonDocument.Parse(payloadNode.ToJsonString());

        Assert.NotEmpty(schemaSet.Validate(
            "cli-output/payload/screenshot.scene.schema.json",
            payload.RootElement));
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
        bool hasRequestedResolution = true,
        string lifecycleStateAtCapture = "ready",
        string playModeState = "stopped")
    {
        var requestedWidth = hasRequestedResolution ? "1920" : "null";
        var requestedHeight = hasRequestedResolution ? "1080" : "null";
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
            "requestedWidth": {{requestedWidth}},
            "requestedHeight": {{requestedHeight}},
            "width": 1920,
            "height": 1080,
            "colorSpace": "linear",
            "lifecycleStateAtCapture": "{{lifecycleStateAtCapture}}",
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
