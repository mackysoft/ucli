using System.Text.Json.Nodes;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Hosting.Cli.Schemas;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Schemas;

public sealed class UcliStaticSchemaSetTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void Load_WhenSchemaBytesDoNotMatchManifestHash_RejectsSet ()
    {
        using var scope = TestDirectories.CreateTempScope("static-schema-set", "hash-mismatch");
        var schemaRoot = StaticSchemaSetTestSupport.CopyRepositorySchemaSet(scope);
        var entry = StaticSchemaSetTestSupport.GetFirstEntry(
            StaticSchemaSetTestSupport.ReadManifest(schemaRoot));
        var schemaPath = Path.Combine(schemaRoot, entry["path"]!.GetValue<string>());
        File.AppendAllText(schemaPath, " ");

        Assert.Throws<InvalidDataException>(() => UcliStaticSchemaSetLoader.Load(AbsolutePath.Parse(schemaRoot)));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Load_WhenManifestPathEscapesSchemaRoot_RejectsSet ()
    {
        using var scope = TestDirectories.CreateTempScope("static-schema-set", "path-escape");
        var schemaRoot = StaticSchemaSetTestSupport.CopyRepositorySchemaSet(scope);
        var manifest = StaticSchemaSetTestSupport.ReadManifest(schemaRoot);
        StaticSchemaSetTestSupport.GetFirstEntry(manifest)["path"] = "../outside.schema.json";
        StaticSchemaSetTestSupport.WriteManifest(schemaRoot, manifest);

        Assert.Throws<InvalidDataException>(() => UcliStaticSchemaSetLoader.Load(AbsolutePath.Parse(schemaRoot)));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Load_WhenManifestRepeatsLogicalName_RejectsSet ()
    {
        using var scope = TestDirectories.CreateTempScope("static-schema-set", "duplicate-name");
        var schemaRoot = StaticSchemaSetTestSupport.CopyRepositorySchemaSet(scope);
        var manifest = StaticSchemaSetTestSupport.ReadManifest(schemaRoot);
        var entries = manifest["schemas"]!.AsArray();
        entries[1]!["name"] = entries[0]!["name"]!.GetValue<string>();
        StaticSchemaSetTestSupport.WriteManifest(schemaRoot, manifest);

        Assert.Throws<InvalidDataException>(() => UcliStaticSchemaSetLoader.Load(AbsolutePath.Parse(schemaRoot)));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Load_WhenManifestAndDocumentIdsAgreeButDoNotMatchPath_RejectsSet ()
    {
        using var scope = TestDirectories.CreateTempScope("static-schema-set", "non-canonical-id");
        var schemaRoot = StaticSchemaSetTestSupport.CopyRepositorySchemaSet(scope);
        var manifest = StaticSchemaSetTestSupport.ReadManifest(schemaRoot);
        var entry = StaticSchemaSetTestSupport.GetFirstEntry(manifest);
        var schemaPath = Path.Combine(
            schemaRoot,
            entry["path"]!.GetValue<string>());
        var schema = JsonNode.Parse(File.ReadAllText(schemaPath))!.AsObject();
        const string nonCanonicalId = "https://schemas.example.invalid/ucli/schema.json";
        schema["$id"] = nonCanonicalId;
        File.WriteAllText(schemaPath, schema.ToJsonString());
        entry["$id"] = nonCanonicalId;
        entry["sha256"] = Sha256Digest
            .Compute(File.ReadAllBytes(schemaPath))
            .ToString();
        StaticSchemaSetTestSupport.WriteManifest(schemaRoot, manifest);

        Assert.Throws<InvalidDataException>(
            () => UcliStaticSchemaSetLoader.Load(
                AbsolutePath.Parse(schemaRoot)));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Load_WhenManifestContainsDuplicateJsonProperty_RejectsSet ()
    {
        using var scope = TestDirectories.CreateTempScope("static-schema-set", "duplicate-property");
        var schemaRoot = StaticSchemaSetTestSupport.CopyRepositorySchemaSet(scope);
        var manifestPath = Path.Combine(schemaRoot, "schema-manifest.json");
        var manifest = File.ReadAllText(manifestPath);
        File.WriteAllText(
            manifestPath,
            manifest.Replace(
                "\"schemaSet\": \"ucli\",",
                "\"schemaSet\": \"ucli\", \"schemaSet\": \"ucli\",",
                StringComparison.Ordinal));

        Assert.Throws<InvalidDataException>(() => UcliStaticSchemaSetLoader.Load(AbsolutePath.Parse(schemaRoot)));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Load_WhenManifestIsNotValidJson_RejectsSet ()
    {
        using var scope = TestDirectories.CreateTempScope("static-schema-set", "invalid-json");
        var schemaRoot = StaticSchemaSetTestSupport.CopyRepositorySchemaSet(scope);
        File.WriteAllText(Path.Combine(schemaRoot, "schema-manifest.json"), "{");

        Assert.Throws<InvalidDataException>(() => UcliStaticSchemaSetLoader.Load(AbsolutePath.Parse(schemaRoot)));
    }
}
