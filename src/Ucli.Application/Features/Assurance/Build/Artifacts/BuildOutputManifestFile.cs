namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents one regular file in a build output manifest. </summary>
internal sealed record BuildOutputManifestFile (
    string Path,
    long SizeBytes,
    string Sha256);
