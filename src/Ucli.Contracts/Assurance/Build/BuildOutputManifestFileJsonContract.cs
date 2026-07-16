using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Represents one regular file entry in <c>output-manifest.json</c>. </summary>
/// <param name="EntryId"> The referenced output source entry id. </param>
/// <param name="LogicalPath"> The manifest-local slash-separated file path. </param>
/// <param name="SourcePath"> The normalized absolute source file path before artifact-store ingestion. </param>
/// <param name="ArtifactPath"> The artifact-root-relative slash-separated output file path. </param>
/// <param name="SizeBytes"> The file size in bytes. </param>
/// <param name="Sha256"> The lowercase SHA-256 digest for the file content. </param>
internal sealed record BuildOutputManifestFileJsonContract
{
    [JsonConstructor]
    public BuildOutputManifestFileJsonContract (
        string EntryId,
        string LogicalPath,
        string SourcePath,
        string ArtifactPath,
        long SizeBytes,
        Sha256Digest Sha256)
    {
        this.EntryId = ContractArgumentGuard.RequireValue(EntryId, nameof(EntryId));
        this.LogicalPath = ContractArgumentGuard.RequireValue(LogicalPath, nameof(LogicalPath));
        this.SourcePath = ContractArgumentGuard.RequireValue(SourcePath, nameof(SourcePath));
        this.ArtifactPath = ContractArgumentGuard.RequireValue(ArtifactPath, nameof(ArtifactPath));
        this.SizeBytes = SizeBytes >= 0
            ? SizeBytes
            : throw new ArgumentOutOfRangeException(nameof(SizeBytes), SizeBytes, "File size must not be negative.");
        this.Sha256 = Sha256 ?? throw new ArgumentNullException(nameof(Sha256));
    }

    public string EntryId { get; }

    public string LogicalPath { get; }

    public string SourcePath { get; }

    public string ArtifactPath { get; }

    public long SizeBytes { get; }

    public Sha256Digest Sha256 { get; }
}
