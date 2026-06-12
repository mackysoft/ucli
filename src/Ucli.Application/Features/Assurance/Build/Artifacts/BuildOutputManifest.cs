using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents a persisted build output manifest. </summary>
internal sealed record BuildOutputManifest (
    int SchemaVersion,
    string OutputRoot,
    string Target,
    int FileCount,
    long TotalBytes,
    IReadOnlyList<BuildOutputManifestFile> Files,
    [property: JsonIgnore]
    string ManifestDigest);
