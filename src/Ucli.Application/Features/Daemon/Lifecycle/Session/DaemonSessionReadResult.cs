using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Represents one internally consistent daemon session storage read outcome. </summary>
internal sealed class DaemonSessionReadResult
{
    private DaemonSessionReadResult (
        DaemonSession? session,
        DaemonInvalidSessionEvidence? invalidEvidence,
        ExecutionError? error,
        DaemonSessionReadFailureKind failureKind,
        DaemonSessionArtifactIdentity? artifactIdentity)
    {
        Session = session;
        InvalidEvidence = invalidEvidence;
        Error = error;
        FailureKind = failureKind;
        ArtifactIdentity = artifactIdentity;
    }

    /// <summary> Gets the validated session only for a successful found result. </summary>
    public DaemonSession? Session { get; }

    /// <summary> Gets restricted untrusted evidence only for a parsed invalid result. </summary>
    public DaemonInvalidSessionEvidence? InvalidEvidence { get; }

    /// <summary> Gets the read error for a failed result. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets the categorized failure kind. </summary>
    public DaemonSessionReadFailureKind FailureKind { get; }

    /// <summary> Gets the exact serialized artifact identity when a file was observed. </summary>
    public DaemonSessionArtifactIdentity? ArtifactIdentity { get; }

    /// <summary> Gets a value indicating whether the read completed without error. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Gets a value indicating whether a validated session exists. </summary>
    public bool Exists => Session is not null;

    /// <summary> Creates a successful result for an absent session artifact. </summary>
    /// <returns> The missing result. </returns>
    public static DaemonSessionReadResult Missing ()
    {
        return new DaemonSessionReadResult(null, null, null, DaemonSessionReadFailureKind.None, null);
    }

    /// <summary> Creates a successful result for one validated observed session artifact. </summary>
    /// <param name="session"> The validated runtime session. </param>
    /// <param name="artifactIdentity"> The exact observed artifact identity. </param>
    /// <returns> The found result. </returns>
    public static DaemonSessionReadResult Found (
        DaemonSession session,
        DaemonSessionArtifactIdentity artifactIdentity)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(artifactIdentity);
        return new DaemonSessionReadResult(session, null, null, DaemonSessionReadFailureKind.None, artifactIdentity);
    }

    /// <summary> Creates an invalid-session result for one observed artifact. </summary>
    /// <param name="error"> The invalid-session error. </param>
    /// <param name="invalidEvidence"> The restricted parsed evidence when JSON deserialization succeeded. </param>
    /// <param name="artifactIdentity"> The exact observed artifact identity. </param>
    /// <returns> The invalid-session result. </returns>
    public static DaemonSessionReadResult Invalid (
        ExecutionError error,
        DaemonInvalidSessionEvidence? invalidEvidence,
        DaemonSessionArtifactIdentity artifactIdentity)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(artifactIdentity);
        return new DaemonSessionReadResult(
            null,
            invalidEvidence,
            error,
            DaemonSessionReadFailureKind.InvalidSession,
            artifactIdentity);
    }

    /// <summary> Creates a non-validation storage failure. </summary>
    /// <param name="error"> The storage error. </param>
    /// <param name="failureKind"> The non-validation failure kind. </param>
    /// <param name="artifactIdentity"> The exact observed artifact identity when available. </param>
    /// <returns> The failed result. </returns>
    public static DaemonSessionReadResult Failure (
        ExecutionError error,
        DaemonSessionReadFailureKind failureKind,
        DaemonSessionArtifactIdentity? artifactIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (failureKind is DaemonSessionReadFailureKind.None or DaemonSessionReadFailureKind.InvalidSession)
        {
            throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, "Use a state-specific factory for successful or invalid-session results.");
        }

        return new DaemonSessionReadResult(null, null, error, failureKind, artifactIdentity);
    }
}
