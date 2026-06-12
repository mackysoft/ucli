using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents one persisted build artifact write result. </summary>
internal sealed record BuildArtifactWriteResult (
    string? Digest,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether artifact writing succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful artifact write result. </summary>
    public static BuildArtifactWriteResult Success (string digest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(digest);
        return new BuildArtifactWriteResult(digest, null);
    }

    /// <summary> Creates a failed artifact write result. </summary>
    public static BuildArtifactWriteResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new BuildArtifactWriteResult(null, error);
    }
}
