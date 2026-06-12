namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Represents one regular file entry in <c>output-manifest.json</c>. </summary>
/// <param name="Path"> The output-root-relative slash-separated file path. </param>
/// <param name="SizeBytes"> The file size in bytes. </param>
/// <param name="Sha256"> The lowercase SHA-256 digest for the file content. </param>
internal sealed record BuildOutputManifestFileJsonContract (
    string Path,
    long SizeBytes,
    string Sha256);
