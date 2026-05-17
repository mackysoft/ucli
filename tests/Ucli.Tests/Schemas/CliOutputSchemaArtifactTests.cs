using System.Text.Json;
using MackySoft.Tests;

namespace MackySoft.Ucli.Tests.Schemas;

public sealed class CliOutputSchemaArtifactTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    [Trait("Size", "Small")]
    public void SchemaManifest_IndexesGeneratedV1Schemas ()
    {
        var schemaRoot = Path.Combine(RepositoryRoot, "schemas", "v1");
        var manifestPath = Path.Combine(schemaRoot, "schema-manifest.json");

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
                File.Exists(Path.Combine(schemaRoot, path!)),
                $"Schema manifest references missing schema path: {path}");
            Assert.DoesNotContain("v1", Path.GetFileName(path), StringComparison.OrdinalIgnoreCase);

            if (schemaEntry.TryGetProperty("command", out var commandElement))
            {
                commandEntries.Add(commandElement.GetString()!, path!);
            }
        }

        Assert.Contains("status", commandEntries.Keys);
        Assert.Contains("ready", commandEntries.Keys);
        Assert.Contains("plan", commandEntries.Keys);
        Assert.Contains("ops.describe", commandEntries.Keys);
        Assert.Contains("codes.describe", commandEntries.Keys);
        Assert.Contains("test.run", commandEntries.Keys);
    }

    [Theory]
    [MemberData(nameof(GetCliOutputGoldenFiles))]
    [Trait("Size", "Small")]
    public void CliOutputGoldenFile_MatchesEnvelopeAndCommandPayloadSchemas (string repositoryRelativeGoldenPath)
    {
        using var schemaSet = JsonSchemaArtifactSet.Load(Path.Combine(RepositoryRoot, "schemas", "v1"));
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, repositoryRelativeGoldenPath)));
        var root = document.RootElement;

        Assert.False(root.TryGetProperty("schemaVersion", out _));
        AssertSchemaValid(schemaSet.Validate("cli-output/envelope.schema.json", root), repositoryRelativeGoldenPath);

        var command = root.GetProperty("command").GetString();
        Assert.False(string.IsNullOrWhiteSpace(command));
        var payloadSchemaPath = schemaSet.FindPayloadSchemaPath(command!);
        Assert.False(
            string.IsNullOrWhiteSpace(payloadSchemaPath),
            $"No payload schema is registered for command '{command}' in {repositoryRelativeGoldenPath}.");

        AssertSchemaValid(
            schemaSet.Validate(payloadSchemaPath!, root.GetProperty("payload"), "$.payload"),
            repositoryRelativeGoldenPath);
    }

    [Theory]
    [MemberData(nameof(GetReportRefContractCases))]
    [Trait("Size", "Small")]
    public void ReportRefSchema_RequiresKindAndExactlyOneLocation (
        string reportJson,
        bool expectedValid)
    {
        using var schemaSet = JsonSchemaArtifactSet.Load(Path.Combine(RepositoryRoot, "schemas", "v1"));
        using var document = JsonDocument.Parse(reportJson);

        var errors = schemaSet.Validate("cli-output/defs/report-ref.schema.json", document.RootElement);

        if (expectedValid)
        {
            Assert.Empty(errors);
        }
        else
        {
            Assert.NotEmpty(errors);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadyPayloadSchema_RequiresClaimValidity ()
    {
        using var schemaSet = JsonSchemaArtifactSet.Load(Path.Combine(RepositoryRoot, "schemas", "v1"));
        using var document = JsonDocument.Parse(
            """
            {
              "verdict": "pass",
              "project": {
                "projectPath": "/repo/UnityProject",
                "projectFingerprint": "project-fingerprint",
                "unityVersion": "6000.1.4f1"
              },
              "verifiers": [
                {
                  "id": "ready.lifecycle",
                  "kind": "ready.lifecycle",
                  "deterministic": false,
                  "required": true,
                  "primaryClaims": [
                    "UNITY_READY_EXECUTION"
                  ],
                  "effects": []
                }
              ],
              "claims": [
                {
                  "id": "UNITY_READY_EXECUTION",
                  "status": "passed",
                  "coverage": "full",
                  "required": true,
                  "verifierRef": "ready.lifecycle",
                  "statement": "Unity is ready for execution.",
                  "subject": {},
                  "evidence": [],
                  "residualRisks": []
                }
              ],
              "reports": {},
              "residualRisks": [],
              "target": "execution",
              "requestedMode": "auto",
              "resolvedMode": "oneshot",
              "sessionKind": "transientProbe",
              "timeoutMilliseconds": 10000,
              "lifecycle": null,
              "readIndex": null
            }
            """);

        var errors = schemaSet.Validate(
            "cli-output/payload/ready.schema.json",
            document.RootElement);

        Assert.NotEmpty(errors);
    }

    public static IEnumerable<object[]> GetCliOutputGoldenFiles ()
    {
        var goldenRoot = Path.Combine(RepositoryRoot, "tests", "Ucli.Tests", "GoldenFiles", "Json", "CliOutput");
        return Directory
            .EnumerateFiles(goldenRoot, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => new object[]
            {
                Path.GetRelativePath(RepositoryRoot, path),
            });
    }

    public static IEnumerable<object[]> GetReportRefContractCases ()
    {
        yield return new object[]
        {
            """
            {
              "kind": "log",
              "path": "artifacts/ready.log"
            }
            """,
            true,
        };
        yield return new object[]
        {
            """
            {
              "kind": "report",
              "uri": "https://example.test/report"
            }
            """,
            true,
        };
        yield return new object[]
        {
            """
            {
              "kind": "report",
              "digest": "sha256:abc"
            }
            """,
            false,
        };
        yield return new object[]
        {
            """
            {
              "kind": "report",
              "path": "artifacts/ready.log",
              "uri": "https://example.test/report"
            }
            """,
            false,
        };
    }

    private static void AssertSchemaValid (
        IReadOnlyList<string> errors,
        string repositoryRelativeGoldenPath)
    {
        Assert.True(
            errors.Count == 0,
            $"Schema validation failed for {repositoryRelativeGoldenPath}:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
    }

    private static string FindRepositoryRoot ()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ucli.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved from test base directory.");
    }
}
