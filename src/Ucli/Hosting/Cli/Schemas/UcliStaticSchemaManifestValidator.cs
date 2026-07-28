using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Schemas;

namespace MackySoft.Ucli.Hosting.Cli.Schemas;

/// <summary> Validates the product-owned index of one uCLI static schema set. </summary>
internal static class UcliStaticSchemaManifestValidator
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters =
        {
            new VocabularyJsonConverterFactory(),
        },
    };

    public static UcliStaticSchemaManifest Deserialize (byte[] utf8)
    {
        using var document = UniqueJsonDocumentParser.Parse(utf8, "Static schema manifest");
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Static schema manifest must contain a JSON object.");
        }

        return document.RootElement.Deserialize<UcliStaticSchemaManifest>(SerializerOptions);
    }

    public static IReadOnlyList<UcliStaticSchemaArtifactLocation> Validate (
        UcliStaticSchemaManifest manifest)
    {
        ValidateHeader(manifest);

        var entries = manifest.Schemas
            ?? throw new InvalidDataException("Static schema manifest schemas must not be null.");
        UcliStaticSchemaManifestRelationshipValidator.EnsureEntryOrder(entries);
        var index = new UcliStaticSchemaManifestEntryIndex(entries.Count);
        for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            index.Add(
                entries[entryIndex]
                    ?? throw new InvalidDataException(
                        $"Static schema manifest entry {entryIndex} must not be null."));
        }

        index.EnsureDependenciesResolve(entries);
        return index.Locations;
    }

    private static void ValidateHeader (UcliStaticSchemaManifest manifest)
    {
        if (manifest.SchemaSet != UcliStaticSchemaSetName.Ucli
            || string.IsNullOrWhiteSpace(manifest.PackageVersion)
            || manifest.ProtocolVersion != IpcProtocol.CurrentVersion
            || manifest.JsonSchemaDialect != UcliJsonSchemaDialect.Draft202012)
        {
            throw new InvalidDataException("Static schema manifest header does not match the running uCLI contract.");
        }
    }

}

/// <summary> Builds the searchable, uniqueness-checked index of manifest entries. </summary>
internal sealed class UcliStaticSchemaManifestEntryIndex
{
    private readonly HashSet<string> names = new(StringComparer.Ordinal);
    private readonly HashSet<string> paths = new(StringComparer.Ordinal);
    private readonly HashSet<string> ids = new(StringComparer.Ordinal);
    private readonly HashSet<string> payloadSelections = new(StringComparer.Ordinal);
    private readonly List<UcliStaticSchemaArtifactLocation> locations;

    public UcliStaticSchemaManifestEntryIndex (int capacity)
    {
        locations = new List<UcliStaticSchemaArtifactLocation>(capacity);
    }

    public IReadOnlyList<UcliStaticSchemaArtifactLocation> Locations => locations;

    public void Add (UcliStaticSchemaEntry entry)
    {
        ValidateRequiredValues(entry);
        var path = ParseRelativePath(entry.Path);
        EnsureUniqueIdentity(entry, path);
        EnsurePayloadSelection(entry);
        UcliStaticSchemaManifestRelationshipValidator.EnsureEntryCollections(entry);
        locations.Add(new UcliStaticSchemaArtifactLocation(entry, path));
    }

    public void EnsureDependenciesResolve (
        IReadOnlyList<UcliStaticSchemaEntry> entries)
    {
        UcliStaticSchemaManifestRelationshipValidator.EnsureDependenciesResolve(
            entries,
            names);
    }

    private static void ValidateRequiredValues (UcliStaticSchemaEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Name)
            || string.IsNullOrWhiteSpace(entry.Path)
            || string.IsNullOrWhiteSpace(entry.Id)
            || entry.Sha256 == null
            || entry.Usages == null
            || entry.StaticDependencies == null
            || entry.DynamicValidationSources == null)
        {
            throw new InvalidDataException(
                "Static schema manifest entries must contain every required value.");
        }
    }

    private void EnsureUniqueIdentity (
        UcliStaticSchemaEntry entry,
        RootRelativePath path)
    {
        var expectedId = UcliStaticSchemaSet.CreateSchemaId(path);
        if (!string.Equals(entry.Id, expectedId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Static schema entry '{entry.Name}' must use canonical $id '{expectedId}'.");
        }

        if (!names.Add(entry.Name)
            || !paths.Add(path.Value)
            || !ids.Add(entry.Id))
        {
            throw new InvalidDataException(
                "Static schema manifest names, paths, and $id values must be unique.");
        }
    }

    private void EnsurePayloadSelection (UcliStaticSchemaEntry entry)
    {
        var isPayload = entry.Kind == UcliStaticSchemaKind.CliOutputPayload;
        var hasSelection = !string.IsNullOrWhiteSpace(entry.Command)
            && entry.Status.HasValue
            && TextVocabulary.IsDefined(entry.Status.Value);
        if (isPayload != hasSelection)
        {
            throw new InvalidDataException(
                $"Static schema entry '{entry.Name}' has an invalid command/status classification.");
        }

        if (isPayload
            && !payloadSelections.Add(
                entry.Command + "\0" + TextVocabulary.GetText(entry.Status!.Value)))
        {
            throw new InvalidDataException(
                $"Static schema entry '{entry.Name}' duplicates a command/status selection.");
        }
    }

    private static RootRelativePath ParseRelativePath (string value)
    {
        if (value.Contains('\\')
            || !RootRelativePath.TryParse(value, out var path, out _)
            || path.IsRoot
            || !string.Equals(path.Value, value, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Static schema path '{value}' must be a normalized relative path.");
        }

        return path;
    }
}

internal sealed record UcliStaticSchemaArtifactLocation (
    UcliStaticSchemaEntry Entry,
    RootRelativePath RelativePath);
