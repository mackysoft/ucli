using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Schemas;

namespace MackySoft.Ucli.Hosting.Cli.Schemas;

/// <summary> Indexes one validated immutable static schema artifact set. </summary>
internal sealed class UcliStaticSchemaSet
{
    /// <summary> Gets the canonical base identifier shared by every schema in this set. </summary>
    internal const string SchemaBaseId = "https://schemas.mackysoft.dev/ucli/";

    private readonly IReadOnlyDictionary<string, UcliStoredStaticSchemaArtifact> artifactsByName;

    public UcliStaticSchemaSet (
        UcliStaticSchemaManifest manifest,
        byte[] manifestUtf8,
        IReadOnlyList<UcliStoredStaticSchemaArtifact> artifacts)
    {
        Manifest = manifest;
        ManifestUtf8 = manifestUtf8 ?? throw new ArgumentNullException(nameof(manifestUtf8));
        Artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
        artifactsByName = artifacts.ToDictionary(
            static artifact => artifact.Entry.Name!,
            StringComparer.Ordinal);
    }

    /// <summary> Gets the validated manifest. </summary>
    public UcliStaticSchemaManifest Manifest { get; }

    internal byte[] ManifestUtf8 { get; }

    internal IReadOnlyList<UcliStoredStaticSchemaArtifact> Artifacts { get; }

    /// <summary> Creates the canonical document identifier for one schema-set-relative path. </summary>
    internal static string CreateSchemaId (RootRelativePath relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        return SchemaBaseId + relativePath.Value;
    }

    /// <summary> Gets one schema by exact logical name. </summary>
    /// <param name="name"> Exact manifest logical name. </param>
    /// <returns> The schema artifact, or <see langword="null" /> when the name is unknown. </returns>
    public UcliStaticSchemaArtifact? Find (string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!artifactsByName.TryGetValue(name, out var artifact))
        {
            return null;
        }

        using var document = JsonDocument.Parse(artifact.Utf8);
        return new UcliStaticSchemaArtifact(artifact.Entry, document.RootElement.Clone());
    }
}

/// <summary> Holds one validated static schema manifest entry and parsed document. </summary>
internal sealed record UcliStaticSchemaArtifact (
    UcliStaticSchemaEntry Entry,
    JsonElement Document);

internal sealed record UcliStoredStaticSchemaArtifact (
    UcliStaticSchemaEntry Entry,
    RootRelativePath RelativePath,
    byte[] Utf8);
