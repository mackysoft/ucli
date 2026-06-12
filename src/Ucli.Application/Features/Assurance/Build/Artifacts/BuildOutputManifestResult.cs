using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents output manifest generation result. </summary>
internal sealed record BuildOutputManifestResult (
    BuildOutputManifest? Manifest,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether manifest generation succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful manifest result. </summary>
    public static BuildOutputManifestResult Success (BuildOutputManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return new BuildOutputManifestResult(manifest, null);
    }

    /// <summary> Creates a failed manifest result. </summary>
    public static BuildOutputManifestResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new BuildOutputManifestResult(null, error);
    }
}
