namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Represents the digest source fields of <c>output-manifest.json</c>. </summary>
/// <param name="SchemaVersion"> The output-manifest schema version. </param>
/// <param name="Target"> The resolved build target identity. </param>
/// <param name="Entries"> The output source entries in resolved ordinal order. </param>
/// <param name="EntryCount"> The number of output source entries included in the manifest. </param>
/// <param name="FileCount"> The number of regular files included in the manifest. </param>
/// <param name="TotalBytes"> The total byte count for manifest files. </param>
/// <param name="Files"> The manifest file entries in canonical order. </param>
internal sealed record BuildOutputManifestContentJsonContract (
    int SchemaVersion,
    BuildOutputManifestTargetJsonContract Target,
    IReadOnlyList<BuildOutputManifestEntryJsonContract> Entries,
    int EntryCount,
    int FileCount,
    long TotalBytes,
    IReadOnlyList<BuildOutputManifestFileJsonContract> Files);
