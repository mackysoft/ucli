using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Represents the persisted <c>output-manifest.json</c> contract. </summary>
/// <param name="SchemaVersion"> The output-manifest schema version. </param>
/// <param name="Target"> The resolved build target identity. </param>
/// <param name="Entries"> The output source entries in resolved ordinal order. </param>
/// <param name="EntryCount"> The number of output source entries included in the manifest. </param>
/// <param name="FileCount"> The number of regular files included in the manifest. </param>
/// <param name="TotalBytes"> The total byte count for manifest files. </param>
/// <param name="Files"> The manifest file entries in canonical order. </param>
/// <param name="ManifestDigest"> The lowercase SHA-256 digest of canonical manifest content excluding this field. </param>
internal sealed record BuildOutputManifestJsonContract
{
    /// <summary> Gets the current output-manifest schema version. </summary>
    public const int CurrentSchemaVersion = 1;

    [JsonConstructor]
    public BuildOutputManifestJsonContract (
        int SchemaVersion,
        BuildOutputManifestTargetJsonContract Target,
        IReadOnlyList<BuildOutputManifestEntryJsonContract> Entries,
        int EntryCount,
        int FileCount,
        long TotalBytes,
        IReadOnlyList<BuildOutputManifestFileJsonContract> Files,
        Sha256Digest ManifestDigest)
    {
        if (SchemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SchemaVersion), SchemaVersion, "Schema version must be positive.");
        }

        this.Target = Target ?? throw new ArgumentNullException(nameof(Target));
        var entries = Entries?.ToArray() ?? throw new ArgumentNullException(nameof(Entries));
        var files = Files?.ToArray() ?? throw new ArgumentNullException(nameof(Files));
        if (entries.Any(static entry => entry == null))
        {
            throw new ArgumentException("Manifest entries must not contain null items.", nameof(Entries));
        }

        if (files.Any(static file => file == null))
        {
            throw new ArgumentException("Manifest files must not contain null items.", nameof(Files));
        }

        if (EntryCount != entries.Length)
        {
            throw new ArgumentException("Entry count must match the manifest entry collection.", nameof(EntryCount));
        }

        if (FileCount != files.Length)
        {
            throw new ArgumentException("File count must match the manifest file collection.", nameof(FileCount));
        }

        if (TotalBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TotalBytes), TotalBytes, "Total bytes must not be negative.");
        }

        this.SchemaVersion = SchemaVersion;
        this.Entries = Array.AsReadOnly(entries);
        this.EntryCount = EntryCount;
        this.FileCount = FileCount;
        this.TotalBytes = TotalBytes;
        this.Files = Array.AsReadOnly(files);
        this.ManifestDigest = ManifestDigest ?? throw new ArgumentNullException(nameof(ManifestDigest));
    }

    public int SchemaVersion { get; }

    public BuildOutputManifestTargetJsonContract Target { get; }

    public IReadOnlyList<BuildOutputManifestEntryJsonContract> Entries { get; }

    public int EntryCount { get; }

    public int FileCount { get; }

    public long TotalBytes { get; }

    public IReadOnlyList<BuildOutputManifestFileJsonContract> Files { get; }

    public Sha256Digest ManifestDigest { get; }

    /// <summary> Creates the digest source content for this manifest. </summary>
    /// <returns> The digest source content without <c>manifestDigest</c>. </returns>
    public BuildOutputManifestContentJsonContract ToContent ()
    {
        return new BuildOutputManifestContentJsonContract(
            SchemaVersion,
            Target,
            Entries,
            EntryCount,
            FileCount,
            TotalBytes,
            Files);
    }
}
