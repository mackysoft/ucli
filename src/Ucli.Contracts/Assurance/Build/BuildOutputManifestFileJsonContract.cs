namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Represents one regular file entry in <c>output-manifest.json</c>. </summary>
/// <param name="EntryId"> The referenced output source entry id. </param>
/// <param name="LogicalPath"> The manifest-local slash-separated file path. </param>
/// <param name="SourcePath"> The normalized absolute source file path before artifact-store ingestion. </param>
/// <param name="ArtifactPath"> The artifact-root-relative slash-separated output file path. </param>
/// <param name="SizeBytes"> The file size in bytes. </param>
/// <param name="Sha256"> The lowercase SHA-256 digest for the file content. </param>
internal sealed record BuildOutputManifestFileJsonContract (
    string EntryId,
    string LogicalPath,
    string SourcePath,
    string ArtifactPath,
    long SizeBytes,
    string Sha256);
