using System.Text.Json;
using Json.Schema;
using Json.Schema.Keywords;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Schemas;

namespace MackySoft.Ucli.Hosting.Cli.Schemas;

/// <summary> Validates one self-contained generated JSON Schema artifact. </summary>
internal static class UcliStaticSchemaArtifactValidator
{
    private static readonly string ExpectedDialect =
        TextVocabulary.GetText(UcliJsonSchemaDialect.Draft202012);

    public static void Validate (
        UcliStaticSchemaEntry entry,
        byte[] artifactUtf8)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(artifactUtf8);
        ValidateDigest(entry, artifactUtf8);

        using var document = UniqueJsonDocumentParser.Parse(
            artifactUtf8,
            $"Static schema '{entry.Name}'");
        ValidateDocumentMetadata(entry, document.RootElement);
        var schema = BuildSchema(entry, document.RootElement);
        ValidateReferences(entry, schema.Root);
    }

    private static void ValidateDigest (
        UcliStaticSchemaEntry entry,
        byte[] artifactUtf8)
    {
        var digest = Sha256Digest.Compute(artifactUtf8);
        if (digest != entry.Sha256)
        {
            throw new InvalidDataException($"Static schema entry '{entry.Name}' does not match its sha256.");
        }
    }

    private static void ValidateDocumentMetadata (
        UcliStaticSchemaEntry entry,
        JsonElement document)
    {
        if (document.ValueKind != JsonValueKind.Object
            || !document.TryGetProperty("$schema", out var dialect)
            || !string.Equals(dialect.GetString(), ExpectedDialect, StringComparison.Ordinal)
            || !document.TryGetProperty("$id", out var id)
            || !string.Equals(id.GetString(), entry.Id, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Static schema entry '{entry.Name}' has inconsistent $schema or $id metadata.");
        }
    }

    private static global::Json.Schema.JsonSchema BuildSchema (
        UcliStaticSchemaEntry entry,
        JsonElement document)
    {
        try
        {
            return global::Json.Schema.JsonSchema.Build(
                document,
                new BuildOptions
                {
                    SchemaRegistry = new SchemaRegistry
                    {
                        Fetch = null!,
                    },
                });
        }
        catch (Exception exception) when (exception is JsonException
                                          or JsonSchemaException
                                          or RefResolutionException
                                          or ArgumentException)
        {
            throw new InvalidDataException(
                $"Static schema entry '{entry.Name}' is not a self-contained JSON Schema.",
                exception);
        }
    }

    private static void ValidateReferences (
        UcliStaticSchemaEntry entry,
        JsonSchemaNode root)
    {
        var pending = new Stack<JsonSchemaNode>();
        var visited = new HashSet<JsonSchemaNode>(ReferenceEqualityComparer.Instance);
        pending.Push(root);
        while (pending.TryPop(out var node))
        {
            if (!visited.Add(node))
            {
                continue;
            }

            foreach (var keyword in node.Keywords)
            {
                ValidateReferenceKeyword(entry, keyword);
                foreach (var subschema in keyword.Subschemas)
                {
                    pending.Push(subschema);
                }
            }
        }
    }

    private static void ValidateReferenceKeyword (
        UcliStaticSchemaEntry entry,
        KeywordData keyword)
    {
        if (keyword.Handler is DynamicRefKeyword)
        {
            throw new InvalidDataException(
                $"Static schema entry '{entry.Name}' contains unsupported $dynamicRef.");
        }

        if (keyword.Handler is not RefKeyword)
        {
            return;
        }

        var reference = keyword.RawValue.GetString()!;
        if (!reference.StartsWith("#", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Static schema entry '{entry.Name}' contains non-fragment $ref '{reference}'.");
        }

        if (!keyword.Subschemas.Any())
        {
            throw new InvalidDataException(
                $"Static schema entry '{entry.Name}' contains unresolved local $ref '{reference}'.");
        }
    }
}
