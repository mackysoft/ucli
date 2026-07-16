using System.Text.Json;
using System.Text.Json.Nodes;

namespace MackySoft.Ucli.Tests.Schemas;

public sealed class CliOutputSchemaArtifactTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void VerifierSchema_RestrictsKindToCanonicalLiterals ()
    {
        var schemaPath = Path.Combine(
            CliOutputSchemaTestSupport.SchemaRoot,
            "cli-output",
            "defs",
            "verifier.schema.json");
        using var document = JsonDocument.Parse(File.ReadAllText(schemaPath));

        var literals = document.RootElement
            .GetProperty("properties")
            .GetProperty("kind")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(static item => item.GetString()!)
            .ToArray();

        Assert.Equal(["ready", "compile", "build", "postRead", "test", "logs"], literals);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void SchemaManifest_IndexesGeneratedV1Schemas ()
    {
        var manifestPath = Path.Combine(CliOutputSchemaTestSupport.SchemaRoot, "schema-manifest.json");

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;
        Assert.Equal("ucli", root.GetProperty("schemaSet").GetString());
        Assert.Equal("v1", root.GetProperty("schemaSetVersion").GetString());
        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal("0.0.0", root.GetProperty("packageVersion").GetString());
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", root.GetProperty("jsonSchemaDialect").GetString());

        var commandEntries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var schemaEntry in root.GetProperty("schemas").EnumerateArray())
        {
            var path = schemaEntry.GetProperty("path").GetString();
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(
                File.Exists(Path.Combine(CliOutputSchemaTestSupport.SchemaRoot, path!)),
                $"Schema manifest references missing schema path: {path}");
            Assert.DoesNotContain("v1", Path.GetFileName(path), StringComparison.OrdinalIgnoreCase);

            if (schemaEntry.TryGetProperty("command", out var commandElement))
            {
                commandEntries.Add(commandElement.GetString()!, path!);
            }
        }

        Assert.Contains("status", commandEntries.Keys);
        Assert.Contains("ready", commandEntries.Keys);
        Assert.Contains("compile", commandEntries.Keys);
        Assert.Contains("build.run", commandEntries.Keys);
        Assert.Contains("verify", commandEntries.Keys);
        Assert.Contains("plan", commandEntries.Keys);
        Assert.Contains("call", commandEntries.Keys);
        Assert.Contains("eval", commandEntries.Keys);
        Assert.Contains("ops.describe", commandEntries.Keys);
        Assert.Contains("codes.describe", commandEntries.Keys);
        Assert.Contains("play.status", commandEntries.Keys);
        Assert.Contains("play.enter", commandEntries.Keys);
        Assert.Contains("play.exit", commandEntries.Keys);
        Assert.Contains("screenshot.game", commandEntries.Keys);
        Assert.Contains("screenshot.scene", commandEntries.Keys);
        Assert.Contains("test.run", commandEntries.Keys);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void CliOutputGoldenFiles_MatchEnvelopeAndCommandPayloadSchemas ()
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        foreach (CliOutputGoldenFiles.GoldenDocument golden in CliOutputGoldenFiles.ReadAllDocuments())
        {
            AssertGoldenFileMatchesEnvelopeAndCommandPayloadSchemas(schemaSet, golden);
        }
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData("build-run", "success.json", "build")]
    [InlineData("compile", "pass-no-reload.json", "compile")]
    [InlineData("test-run", "success.json", null)]
    public void CliOutputPayloadSchema_RejectsEmptyRunId (
        string goldenDirectory,
        string goldenFileName,
        string? runOwnerProperty)
    {
        var goldenPath = TestRepositoryPaths.GetFullPath(
            "tests",
            "Ucli.Tests",
            "GoldenFiles",
            "Json",
            "CliOutput",
            goldenDirectory,
            goldenFileName);
        var root = JsonNode.Parse(File.ReadAllText(goldenPath))!.AsObject();
        var payload = root["payload"]!.AsObject();
        var runOwner = runOwnerProperty == null
            ? payload
            : payload[runOwnerProperty]!.AsObject();
        runOwner["runId"] = Guid.Empty.ToString("D");
        using var document = JsonDocument.Parse(root.ToJsonString());
        var command = document.RootElement.GetProperty("command").GetString()!;
        var payloadSchemaPath = CliOutputSchemaTestSupport.SchemaSet.FindPayloadSchemaPath(command)!;

        var errors = CliOutputSchemaTestSupport.SchemaSet.Validate(
            payloadSchemaPath,
            document.RootElement.GetProperty("payload"));

        Assert.NotEmpty(errors);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData("compile", "pass-no-reload.json")]
    [InlineData("ready", "auto-oneshot-success.json")]
    [InlineData("verify", "default-success.json")]
    public void AssurancePayloadSchemas_RejectInvalidReportDigest (
        string goldenDirectory,
        string goldenFileName)
    {
        var goldenPath = TestRepositoryPaths.GetFullPath(
            "tests",
            "Ucli.Tests",
            "GoldenFiles",
            "Json",
            "CliOutput",
            goldenDirectory,
            goldenFileName);
        var root = JsonNode.Parse(File.ReadAllText(goldenPath))!.AsObject();
        var payload = root["payload"]!.AsObject();
        payload["reports"]!["invalid-digest"] = new JsonObject
        {
            ["path"] = "artifacts/report.json",
            ["digest"] = "not-a-digest",
        };
        using var document = JsonDocument.Parse(root.ToJsonString());
        var command = document.RootElement.GetProperty("command").GetString()!;
        var payloadSchemaPath = CliOutputSchemaTestSupport.SchemaSet.FindPayloadSchemaPath(command)!;

        var errors = CliOutputSchemaTestSupport.SchemaSet.Validate(
            payloadSchemaPath,
            document.RootElement.GetProperty("payload"));

        Assert.NotEmpty(errors);
    }

    private static void AssertGoldenFileMatchesEnvelopeAndCommandPayloadSchemas (
        JsonSchemaArtifactSet schemaSet,
        CliOutputGoldenFiles.GoldenDocument golden)
    {
        try
        {
            var root = golden.Root;

            Assert.False(root.TryGetProperty("schemaVersion", out _));
            AssertSchemaValid(schemaSet.Validate("cli-output/envelope.schema.json", root), golden.RepositoryRelativePath);

            var command = root.GetProperty("command").GetString();
            Assert.False(string.IsNullOrWhiteSpace(command));
            var payloadSchemaPath = schemaSet.FindPayloadSchemaPath(command!);
            Assert.False(
                string.IsNullOrWhiteSpace(payloadSchemaPath),
                $"No payload schema is registered for command '{command}' in {golden.RepositoryRelativePath}.");

            var payload = root.GetProperty("payload");
            if (ShouldValidateCommandPayloadSchema(root, payload))
            {
                AssertSchemaValid(
                    schemaSet.Validate(payloadSchemaPath!, payload, "$.payload"),
                    golden.RepositoryRelativePath);
            }

            if (string.Equals(command, UcliCommandIds.OpsDescribe.Name, StringComparison.Ordinal)
                && payload.TryGetProperty("operation", out var operation))
            {
                OpsDescribePayloadSchemaTestSupport.AssertSchemasUseSupportedSubset(operation);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"CLI output schema contract failed for {golden.RepositoryRelativePath}.", ex);
        }
    }

    private static void AssertSchemaValid (
        IReadOnlyList<string> errors,
        string repositoryRelativeGoldenPath)
    {
        Assert.True(
            errors.Count == 0,
            $"Schema validation failed for {repositoryRelativeGoldenPath}:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
    }

    private static bool ShouldValidateCommandPayloadSchema (JsonElement root, JsonElement payload)
    {
        if (!string.Equals(root.GetProperty("status").GetString(), "error", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var _ in payload.EnumerateObject())
        {
            return true;
        }

        return false;
    }

}
