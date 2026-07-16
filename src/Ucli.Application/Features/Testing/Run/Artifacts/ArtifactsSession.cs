namespace MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;

/// <summary> Represents artifact-session metadata for one test-run execution. </summary>
internal sealed record ArtifactsSession
{
    /// <summary> Initializes the artifact session for one identified test run. </summary>
    /// <param name="runId"> The non-empty generated run identifier. </param>
    /// <param name="paths"> The fixed artifact file paths. </param>
    /// <param name="startedAtUtc"> The UTC start timestamp of the run. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="runId" /> is empty. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="paths" /> is <see langword="null" />. </exception>
    public ArtifactsSession (
        Guid runId,
        ArtifactPaths paths,
        DateTimeOffset startedAtUtc)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(runId));
        }

        RunId = runId;
        Paths = paths ?? throw new ArgumentNullException(nameof(paths));
        StartedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(startedAtUtc, nameof(startedAtUtc));
    }

    /// <summary> Gets the non-empty test run identifier. </summary>
    public Guid RunId { get; }

    public ArtifactPaths Paths { get; }

    public DateTimeOffset StartedAtUtc { get; }
}
