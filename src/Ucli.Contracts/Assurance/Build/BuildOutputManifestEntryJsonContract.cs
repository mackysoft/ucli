namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Represents one output source entry in <c>output-manifest.json</c>. </summary>
/// <param name="Id"> The manifest entry id. </param>
/// <param name="Kind"> The resolved output source filesystem shape. </param>
/// <param name="SourcePath"> The normalized absolute source path before artifact-store ingestion. </param>
internal sealed record BuildOutputManifestEntryJsonContract (
    string Id,
    string Kind,
    string SourcePath);
