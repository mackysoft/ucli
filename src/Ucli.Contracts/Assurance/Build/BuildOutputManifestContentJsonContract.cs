namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Represents the digest source fields of <c>output-manifest.json</c>. </summary>
/// <param name="SchemaVersion"> The output-manifest schema version. </param>
/// <param name="OutputRoot"> The build output root path. </param>
/// <param name="Target"> The resolved build target stable name. </param>
/// <param name="FileCount"> The number of regular files included in the manifest. </param>
/// <param name="TotalBytes"> The total byte count for manifest files. </param>
/// <param name="Files"> The manifest file entries in canonical order. </param>
internal sealed record BuildOutputManifestContentJsonContract (
    int SchemaVersion,
    string OutputRoot,
    string Target,
    int FileCount,
    long TotalBytes,
    IReadOnlyList<BuildOutputManifestFileJsonContract> Files);
