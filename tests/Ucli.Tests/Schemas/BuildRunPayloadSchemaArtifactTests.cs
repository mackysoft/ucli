using System.Text.Json;
using System.Text.Json.Nodes;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Schemas;

public sealed class BuildRunPayloadSchemaArtifactTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void BuildRunPayloadSchema_UsesInputsAndReportRefs ()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(TestRepositoryPaths.GetFullPath(
            "schemas",
            "v1",
            "cli-output",
            "payload",
            "build.run.schema.json")));
        var successSchema = document.RootElement.GetProperty("oneOf")[0];
        var buildProperties = successSchema.GetProperty("properties").GetProperty("build").GetProperty("properties");
        var reportProperties = successSchema.GetProperty("properties").GetProperty("reports").GetProperty("properties");

        Assert.True(buildProperties.TryGetProperty("inputs", out _));
        Assert.False(buildProperties.TryGetProperty("buildTarget", out _));
        Assert.False(buildProperties.TryGetProperty("scenes", out _));
        Assert.False(buildProperties.TryGetProperty("options", out _));
        Assert.Equal(
            TextVocabulary.GetTexts<BuildArtifactKind>(),
            reportProperties.EnumerateObject().Select(static property => property.Name).ToArray());
        foreach (var reportSchema in reportProperties.EnumerateObject())
        {
            var properties = reportSchema.Value.GetProperty("properties");
            Assert.True(properties.TryGetProperty("path", out _));
            Assert.True(properties.TryGetProperty("digest", out _));
            Assert.False(properties.TryGetProperty("kind", out _));
            Assert.False(properties.TryGetProperty("category", out _));
            Assert.False(properties.TryGetProperty("buildResult", out _));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void BuildRunPayloadSchema_RejectsNonStringRunnerArguments ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        var goldenPath = TestRepositoryPaths.GetFullPath(
            "tests",
            "Ucli.Tests",
            "GoldenFiles",
            "Json",
            "CliOutput",
            "build-run",
            "success.json");
        var json = File.ReadAllText(goldenPath).Replace(
            "\"arguments\": {}",
            "\"arguments\": {\"count\": 1}",
            StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);

        var errors = schemaSet.Validate(
            "cli-output/payload/build.run.schema.json",
            document.RootElement.GetProperty("payload"));

        Assert.NotEmpty(errors);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData("runnerResult", "status")]
    [InlineData("summary", "result")]
    public void BuildRunPayloadSchema_RejectsUnknownBuildResult (
        string ownerName,
        string propertyName)
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        var goldenPath = TestRepositoryPaths.GetFullPath(
            "tests",
            "Ucli.Tests",
            "GoldenFiles",
            "Json",
            "CliOutput",
            "build-run",
            "success.json");
        var root = JsonNode.Parse(File.ReadAllText(goldenPath))!.AsObject();
        root["payload"]!["build"]![ownerName]![propertyName] = "unknown";
        using var document = JsonDocument.Parse(root.ToJsonString());

        var errors = schemaSet.Validate(
            "cli-output/payload/build.run.schema.json",
            document.RootElement.GetProperty("payload"));

        Assert.NotEmpty(errors);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData("Assets/BuildProfiles/Linux.asset", true)]
    [InlineData("Assets/BuildProfiles/Linux.asset.meta", false)]
    [InlineData("Assets/BuildProfiles/Linux.asset.META", false)]
    [InlineData("Assets\\BuildProfiles\\Linux.asset", false)]
    [InlineData("Assets//BuildProfiles/Linux.asset", false)]
    [InlineData("Assets/../BuildProfiles/Linux.asset", false)]
    [InlineData("Assets/BuildProfiles/Linux:Dev.asset", false)]
    [InlineData("Assets/BuildProfiles/Linux\t.asset", false)]
    [InlineData("Assets/BuildProfiles/Linux.asset ", false)]
    [InlineData("Packages/BuildProfiles/Linux.asset", false)]
    public void BuildRunPayloadSchema_UnityBuildProfilePathMatchesContract (
        string path,
        bool expectedValid)
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        using var document = JsonDocument.Parse(CreateUnityBuildProfileBuildRunPayloadJson(path));

        var errors = schemaSet.Validate(
            "cli-output/payload/build.run.schema.json",
            document.RootElement);

        Assert.Equal(expectedValid, UnityBuildProfileAssetPath.TryParse(path, out _));
        if (expectedValid)
        {
            Assert.Empty(errors);
        }
        else
        {
            Assert.NotEmpty(errors);
        }
    }

    private static string CreateUnityBuildProfileBuildRunPayloadJson (string path)
    {
        var goldenPath = TestRepositoryPaths.GetFullPath(
            "tests",
            "Ucli.Tests",
            "GoldenFiles",
            "Json",
            "CliOutput",
            "build-run",
            "success.json");
        var root = JsonNode.Parse(File.ReadAllText(goldenPath))!.AsObject();
        var payload = root["payload"]!.AsObject();
        var inputs = payload["build"]!["inputs"]!.AsObject();
        inputs["inputKind"] = "unityBuildProfile";
        inputs["scenes"]!["source"] = "unityBuildProfile";
        inputs["unityBuildProfile"] = new JsonObject
        {
            ["path"] = path,
            ["digest"] = new string('f', 64),
        };
        return payload.ToJsonString();
    }
}
