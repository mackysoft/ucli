using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Init.Common.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Schemas;
using MackySoft.Ucli.Hosting.Cli.Schemas;
using MackySoft.Ucli.Hosting.Composition.Schemas;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Schemas;

public sealed class UcliStaticSchemaGenerationIntegrationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void PayloadRegistrations_SerializeRepresentativeRuntimePayloadsAsObjects ()
    {
        AssertRegisteredPayloadCanSerialize(CommandResult.Success(
            UcliCommandNames.Init,
            "Initialized.",
            new InitExecutionOutput(
                ConfigPath: "/repo/.ucli/config.json",
                GitIgnorePath: "/repo/.ucli/.gitignore")));
        AssertRegisteredPayloadCanSerialize(CommandResult.InternalError(
            UcliCommandNames.Init,
            "Initialization failed."));
        AssertRegisteredPayloadCanSerialize(CommandResult.Success(
            UcliCommandNames.SchemaList,
            "Installed static schemas listed.",
            new UcliStaticSchemaManifest
            {
                SchemaSet = UcliStaticSchemaSetName.Ucli,
                PackageVersion = "0.0.0-test",
                ProtocolVersion = 1,
                JsonSchemaDialect = UcliJsonSchemaDialect.Draft202012,
                Schemas = Array.Empty<UcliStaticSchemaEntry>(),
            }));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Generate_WithUcliRegistrations_MatchesRepositoryAndIsDeterministic ()
    {
        using var scope = TestDirectories.CreateTempScope("static-schema-generation", "repository-artifacts");
        var firstGeneratedRoot = scope.GetPath("generated-first");
        var secondGeneratedRoot = scope.GetPath("generated-second");
        var repositoryRoot = TestRepositoryPaths.GetFullPath(".");
        var repositorySchemaRoot = TestRepositoryPaths.GetFullPath("schemas");
        var packageVersion = StaticSchemaSetTestSupport
            .ReadManifest(repositorySchemaRoot)["packageVersion"]!
            .GetValue<string>();

        UcliStaticSchemaSetGenerator.Generate(
            AbsolutePath.Parse(firstGeneratedRoot),
            AbsolutePath.Parse(repositoryRoot),
            packageVersion);
        UcliStaticSchemaSetGenerator.Generate(
            AbsolutePath.Parse(secondGeneratedRoot),
            AbsolutePath.Parse(repositoryRoot),
            packageVersion);

        AssertArtifactSetsEqual(repositorySchemaRoot, firstGeneratedRoot);
        AssertArtifactSetsEqual(firstGeneratedRoot, secondGeneratedRoot);
    }

    private static void AssertArtifactSetsEqual (
        string expectedRoot,
        string actualRoot)
    {
        var expectedPaths = EnumerateRelativeFilePaths(expectedRoot);
        var actualPaths = EnumerateRelativeFilePaths(actualRoot);
        Assert.Equal(expectedPaths, actualPaths);
        foreach (var relativePath in expectedPaths)
        {
            var expectedBytes = File.ReadAllBytes(Path.Combine(expectedRoot, relativePath));
            var actualBytes = File.ReadAllBytes(Path.Combine(actualRoot, relativePath));
            Assert.Equal(expectedBytes, actualBytes);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void RepositorySchemaSet_IsSelfContainedAndItsManifestMatchesItsSchema ()
    {
        var schemaSet = UcliStaticSchemaSetLoader.Load(
            AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("schemas")));
        Assert.Equal(schemaSet.Manifest.Schemas.Count, schemaSet.Artifacts.Count);
        foreach (var entry in schemaSet.Manifest.Schemas)
        {
            Assert.NotNull(schemaSet.Find(entry.Name!));
        }

        var manifestSchemaArtifact = Assert.IsType<UcliStaticSchemaArtifact>(
            schemaSet.Find("schema.manifest"));
        var manifestSchema = global::Json.Schema.JsonSchema.Build(
            manifestSchemaArtifact.Document,
            new BuildOptions
            {
                SchemaRegistry = new SchemaRegistry
                {
                    Fetch = null!,
                },
            });
        using var manifestDocument = JsonDocument.Parse(schemaSet.ManifestUtf8);

        var result = manifestSchema.Evaluate(manifestDocument.RootElement);

        Assert.True(result.IsValid);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void GeneratedArtifactValidator_RejectsUnresolvedLocalReference ()
    {
        var schemaSet = UcliStaticSchemaSetLoader.Load(
            AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("schemas")));
        var source = schemaSet.Artifacts.Single(
            static artifact => artifact.Entry.Name == "cli-output.envelope");
        var document = JsonNode.Parse(Encoding.UTF8.GetString(source.Utf8))!;
        Assert.True(TryReplaceFirstReference(document, "#/$defs/not-found"));
        var modifiedUtf8 = JsonSerializer.SerializeToUtf8Bytes(document);
        var entry = source.Entry with
        {
            Sha256 = Sha256Digest.Compute(modifiedUtf8),
        };

        var exception = Assert.Throws<InvalidDataException>(
            () => UcliStaticSchemaArtifactValidator.Validate(entry, modifiedUtf8));

        Assert.Contains("unresolved local $ref", exception.Message, StringComparison.Ordinal);
    }

    private static string[] EnumerateRelativeFilePaths (string root)
    {
        return Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertRegisteredPayloadCanSerialize (CommandResult result)
    {
        var registration = UcliCommandPayloadSchemaRegistrationCatalog
            .GetAll()
            .Single(item => item.Command == result.Command && item.Status == result.Status);

        var payload = JsonSerializer.SerializeToElement(
            result.Payload,
            registration.TypeInfo);

        Assert.Equal(JsonValueKind.Object, payload.ValueKind);
    }

    private static bool TryReplaceFirstReference (
        JsonNode node,
        string reference)
    {
        if (node is JsonObject jsonObject)
        {
            if (jsonObject.ContainsKey("$ref"))
            {
                jsonObject["$ref"] = reference;
                return true;
            }

            return jsonObject.Any(property =>
                property.Value != null
                && TryReplaceFirstReference(property.Value, reference));
        }

        return node is JsonArray jsonArray
            && jsonArray.Any(item =>
                item != null
                && TryReplaceFirstReference(item, reference));
    }
}
