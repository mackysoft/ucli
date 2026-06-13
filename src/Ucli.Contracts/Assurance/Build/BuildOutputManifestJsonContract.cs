namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Represents the persisted <c>output-manifest.json</c> contract. </summary>
/// <param name="SchemaVersion"> The output-manifest schema version. </param>
/// <param name="OutputRoot"> The build output root path. </param>
/// <param name="Target"> The resolved build target stable name. </param>
/// <param name="FileCount"> The number of regular files included in the manifest. </param>
/// <param name="TotalBytes"> The total byte count for manifest files. </param>
/// <param name="Files"> The manifest file entries in canonical order. </param>
/// <param name="ManifestDigest"> The lowercase SHA-256 digest of canonical manifest content excluding this field. </param>
internal sealed record BuildOutputManifestJsonContract (
    int SchemaVersion,
    string OutputRoot,
    string Target,
    int FileCount,
    long TotalBytes,
    IReadOnlyList<BuildOutputManifestFileJsonContract> Files,
    string ManifestDigest)
{
    /// <summary> Gets the current output-manifest schema version. </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary> Creates the digest source content for this manifest. </summary>
    /// <returns> The digest source content without <c>manifestDigest</c>. </returns>
    public BuildOutputManifestContentJsonContract ToContent ()
    {
        return new BuildOutputManifestContentJsonContract(
            SchemaVersion,
            OutputRoot,
            Target,
            FileCount,
            TotalBytes,
            Files);
    }
}
