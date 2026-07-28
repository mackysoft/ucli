using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Infrastructure.Artifacts;

/// <summary> Retains the values and final verification required for one repository artifact publication. </summary>
internal sealed class RepositoryArtifactPublication
{
    private readonly ArtifactKind kind;
    private readonly ArtifactMediaType mediaType;
    private readonly Func<DateTimeOffset> getUtcNow;

    private RepositoryArtifactPublication (
        ArtifactKind kind,
        ArtifactMediaType mediaType,
        RepositoryArtifactPublicationPaths paths,
        Func<Stream, CancellationToken, ValueTask> writeSourceAsync,
        Func<Stream, CancellationToken, ValueTask> validateSourceAsync,
        Func<DateTimeOffset> getUtcNow)
    {
        this.kind = kind;
        this.mediaType = mediaType;
        this.getUtcNow = getUtcNow;
        Paths = paths;
        WriteSourceAsync = writeSourceAsync;
        ValidateSourceAsync = validateSourceAsync;
    }

    public RepositoryArtifactPublicationPaths Paths { get; }

    public Func<Stream, CancellationToken, ValueTask> WriteSourceAsync { get; }

    public Func<Stream, CancellationToken, ValueTask> ValidateSourceAsync { get; }

    public static RepositoryArtifactPublication Create (
        ArtifactKind kind,
        ArtifactMediaType mediaType,
        ContainedPath repositoryTemporaryFile,
        ContainedPath repositoryDestinationFile,
        Func<Stream, CancellationToken, ValueTask> writeSourceAsync,
        Func<Stream, CancellationToken, ValueTask> validateSourceAsync,
        Func<DateTimeOffset> getUtcNow)
    {
        return new RepositoryArtifactPublication(
            RequireValue(kind, nameof(kind)),
            RequireValue(mediaType, nameof(mediaType)),
            RepositoryArtifactPublicationPaths.Create(
                RequireValue(repositoryTemporaryFile, nameof(repositoryTemporaryFile)),
                RequireValue(repositoryDestinationFile, nameof(repositoryDestinationFile))),
            writeSourceAsync
                ?? throw new ArgumentNullException(nameof(writeSourceAsync)),
            validateSourceAsync
                ?? throw new ArgumentNullException(nameof(validateSourceAsync)),
            getUtcNow);
    }

    public async ValueTask<PathArtifactRef> CreateReferenceAsync (
        ArtifactPhysicalFileSession source,
        ImmutableArtifactFileReadBoundary.Measurement sourceMeasurement,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var final = source.ReopenSameNodeAlongsideRetainedWriter(cancellationToken);
        var publicationTime = getUtcNow();
        var measurement = await final.MeasureAsync(cancellationToken).ConfigureAwait(false);
        sourceMeasurement.EnsureMatches(
            measurement,
            Paths.DestinationFile.Target,
            "Published artifact final bytes differ from the validated source");
        var artifact = CreateReference(measurement, publicationTime);
        EnsureFinalBinding(source, final);
        return artifact;
    }

    private PathArtifactRef CreateReference (
        ImmutableArtifactFileReadBoundary.Measurement measurement,
        DateTimeOffset publicationTime)
    {
        return new PathArtifactRef(
            kind,
            mediaType,
            Paths.ArtifactPath,
            measurement.Digest,
            measurement.SizeBytes,
            publicationTime);
    }

    private static void EnsureFinalBinding (
        ArtifactPhysicalFileSession source,
        ArtifactPhysicalFileSession final)
    {
        source.EnsureStillBound();
        final.EnsureStillBound();
        source.EnsureSameNodeAs(
            final,
            ImmutableArtifactFilePublisher.DestinationFileSubject);
    }

    private static T RequireValue<T> (T? value, string parameterName)
        where T : class
    {
        return value ?? throw new ArgumentNullException(parameterName);
    }
}
