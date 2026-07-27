using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Schemas;
using MackySoft.Ucli.Hosting.Cli.Schemas;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Schemas;

public sealed class UcliStaticSchemaPayloadContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void PublicObjectRootSchemas_RejectNull ()
    {
        var schemaSet = UcliStaticSchemaSetLoader.Load(
            AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("schemas")));
        var failures = schemaSet.Manifest.Schemas
            .Where(static entry => entry.Kind is
                UcliStaticSchemaKind.SchemaSetMetadata
                or UcliStaticSchemaKind.CliOutputEnvelope
                or UcliStaticSchemaKind.CliOutputPayload)
            .Where(entry => BuildSchema(schemaSet, entry.Name)
                .Evaluate(JsonSerializer.SerializeToElement<object?>(null))
                .IsValid)
            .Select(static entry => entry.Name)
            .ToArray();

        Assert.True(
            failures.Length == 0,
            "The following public object roots accepted null: " + string.Join(", ", failures));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void CliOutputEnvelope_RejectsNullScalarAndArrayPayloads ()
    {
        var schemaSet = UcliStaticSchemaSetLoader.Load(
            AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("schemas")));
        var envelopeSchema = BuildSchema(schemaSet, "cli-output.envelope");
        var golden = CliOutputGoldenFiles.ReadAllDocuments().First();
        var invalidPayloads = new JsonNode?[]
        {
            null,
            JsonValue.Create("text"),
            JsonValue.Create(42),
            new JsonArray(),
        };

        foreach (var invalidPayload in invalidPayloads)
        {
            var instance = JsonNode.Parse(golden.Root.GetRawText())!.AsObject();
            instance["payload"] = invalidPayload;

            var result = envelopeSchema.Evaluate(
                JsonSerializer.SerializeToElement(instance));

            Assert.False(
                result.IsValid,
                $"The CLI output envelope accepted payload '{invalidPayload?.ToJsonString() ?? "null"}'.");
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void EveryCliOutputGolden_AgreesWithEnvelopeAndCommandStatusSchemas ()
    {
        var schemaSet = UcliStaticSchemaSetLoader.Load(
            AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("schemas")));
        var envelopeSchema = BuildSchema(schemaSet, "cli-output.envelope");
        var failures = new List<string>();

        foreach (var golden in CliOutputGoldenFiles.ReadAllDocuments())
        {
            try
            {
                var envelopeFailure = EvaluateGolden(
                    envelopeSchema,
                    golden.Root,
                    golden.RepositoryRelativePath,
                    "cli-output.envelope");
                if (envelopeFailure != null)
                {
                    failures.Add(envelopeFailure);
                    continue;
                }

                var payloadFailure = EvaluateGoldenPayload(schemaSet, golden);
                if (payloadFailure != null)
                {
                    failures.Add(payloadFailure);
                }
            }
            catch (Exception exception)
            {
                failures.Add($"{golden.RepositoryRelativePath}:{Environment.NewLine}{exception}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void GeneratedPublicContracts_RejectMissingAlwaysEmittedProperties ()
    {
        var schemaSet = UcliStaticSchemaSetLoader.Load(
            AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("schemas")));
        var goldens = CliOutputGoldenFiles.ReadAllDocuments();
        var cases = new MissingPropertyCase[]
        {
            new(
                "cli-output.envelope",
                "status/success.json",
                ContainerProperty: null,
                MissingProperty: "errors"),
            new(
                "cli-output.payload.build.run.ok",
                "build-run/success.json",
                ContainerProperty: "payload",
                MissingProperty: "verdict"),
            new(
                "cli-output.payload.test.run.error",
                "test-run/invalid-mode.json",
                ContainerProperty: "payload",
                MissingProperty: "errorKind"),
        };

        foreach (var testCase in cases)
        {
            var golden = goldens.Single(document =>
                document.RepositoryRelativePath.EndsWith(
                    testCase.GoldenPathSuffix,
                    StringComparison.Ordinal));
            var source = testCase.ContainerProperty == null
                ? golden.Root
                : golden.Root.GetProperty(testCase.ContainerProperty);
            var instance = JsonNode.Parse(source.GetRawText())!.AsObject();
            Assert.True(instance.Remove(testCase.MissingProperty));
            var schema = BuildSchema(schemaSet, testCase.LogicalName);

            var result = schema.Evaluate(JsonSerializer.SerializeToElement(instance));

            Assert.False(
                result.IsValid,
                $"'{testCase.LogicalName}' accepted an instance without '{testCase.MissingProperty}'.");
        }
    }

    private static string? EvaluateGoldenPayload (
        UcliStaticSchemaSet schemaSet,
        CliOutputGoldenFiles.GoldenDocument golden)
    {
        var command = ReadRequiredString(golden.Root, "command");
        var status = ReadRequiredString(golden.Root, "status");
        var logicalName = "cli-output.payload." + command + "." + status;
        var schema = BuildSchema(schemaSet, logicalName);
        return EvaluateGolden(
            schema,
            golden.Root.GetProperty("payload"),
            golden.RepositoryRelativePath,
            logicalName);
    }

    private static string? EvaluateGolden (
        global::Json.Schema.JsonSchema schema,
        JsonElement instance,
        string goldenPath,
        string logicalName)
    {
        var result = schema.Evaluate(
            instance,
            new EvaluationOptions
            {
                OutputFormat = OutputFormat.Hierarchical,
            });
        if (result.IsValid)
        {
            return null;
        }

        return $"{goldenPath} was rejected by '{logicalName}':"
            + Environment.NewLine
            + JsonSerializer.Serialize(result);
    }

    private static global::Json.Schema.JsonSchema BuildSchema (
        UcliStaticSchemaSet schemaSet,
        string logicalName)
    {
        var artifact = Assert.IsType<UcliStaticSchemaArtifact>(schemaSet.Find(logicalName));
        return global::Json.Schema.JsonSchema.Build(
            artifact.Document,
            new BuildOptions
            {
                SchemaRegistry = new SchemaRegistry
                {
                    Fetch = null!,
                },
            });
    }

    private static string ReadRequiredString (
        JsonElement element,
        string propertyName)
    {
        var property = element.GetProperty(propertyName);
        Assert.Equal(JsonValueKind.String, property.ValueKind);
        return Assert.IsType<string>(property.GetString());
    }

    private readonly record struct MissingPropertyCase (
        string LogicalName,
        string GoldenPathSuffix,
        string? ContainerProperty,
        string MissingProperty);
}
