using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Infrastructure.Artifacts;

/// <summary> Publishes one feature-validated local file without replacing an existing destination. </summary>
internal sealed class ImmutableArtifactFilePublisher
{
    internal const string TemporaryFileSubject = "Artifact publication temporary file";
    internal const string DestinationFileSubject = "Published artifact file";
    private readonly Func<DateTimeOffset> getUtcNow;

    /// <summary>
    /// Initializes a publisher with the UTC clock captured after create-only publication and before final-byte
    /// measurement.
    /// </summary>
    public ImmutableArtifactFilePublisher (Func<DateTimeOffset> getUtcNow)
    {
        this.getUtcNow = getUtcNow
            ?? throw new ArgumentNullException(nameof(getUtcNow));
    }

    /// <summary>
    /// Creates and owns a private candidate beside the destination, writes and validates one held handle, moves it
    /// create-only, and returns a reference only while the same physical node remains at the final path.
    /// </summary>
    /// <param name="kind"> The product-defined kind of the artifact bytes. </param>
    /// <param name="mediaType"> The media type of the artifact bytes. </param>
    /// <param name="repositoryDestinationFile"> The absent destination in the same repository directory. </param>
    /// <param name="writeSourceAsync">
    /// Writes the candidate bytes to a write-only borrowed stream. Disposing the borrowed view does not dispose the
    /// publisher-owned handle, and the view becomes invalid when the callback completes. The private candidate name
    /// is not exposed to the feature.
    /// </param>
    /// <param name="validateSourceAsync">
    /// Validates a read-only borrowed view of the same handle measured before and after validation. Disposing the view
    /// does not dispose the owned handle, and the view becomes invalid when the callback completes.
    /// </param>
    /// <param name="cancellationToken"> The cancellation token observed during writing, validation, and reads. </param>
    /// <returns> A task that yields a reference measured from the reopened final physical node. </returns>
    /// <remarks>
    /// Failure leaves the private candidate or moved destination in place. The publisher never performs a path-based
    /// check-then-delete that could remove a concurrently substituted foreign node.
    /// </remarks>
    public async ValueTask<PathArtifactRef> PublishAsync (
        ArtifactKind kind,
        ArtifactMediaType mediaType,
        ContainedPath repositoryDestinationFile,
        Func<Stream, CancellationToken, ValueTask> writeSourceAsync,
        Func<Stream, CancellationToken, ValueTask> validateSourceAsync,
        CancellationToken cancellationToken)
    {
        if (kind is null)
        {
            throw new ArgumentNullException(nameof(kind));
        }

        if (mediaType is null)
        {
            throw new ArgumentNullException(nameof(mediaType));
        }

        if (repositoryDestinationFile is null)
        {
            throw new ArgumentNullException(nameof(repositoryDestinationFile));
        }

        if (writeSourceAsync is null)
        {
            throw new ArgumentNullException(nameof(writeSourceAsync));
        }

        if (validateSourceAsync is null)
        {
            throw new ArgumentNullException(nameof(validateSourceAsync));
        }

        using var source = ArtifactPhysicalFileSession.CreateNewBeside(
            repositoryDestinationFile,
            TemporaryFileSubject,
            cancellationToken);
        var publication = RepositoryArtifactPublication.Create(
            kind,
            mediaType,
            source.Request.RepositoryFile,
            repositoryDestinationFile,
            writeSourceAsync,
            validateSourceAsync,
            getUtcNow);
        return await PublishHeldSourceAsync(
            publication,
            source,
            cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<PathArtifactRef> PublishHeldSourceAsync (
        RepositoryArtifactPublication publication,
        ArtifactPhysicalFileSession source,
        CancellationToken cancellationToken)
    {
        await source.WriteAsync(publication.WriteSourceAsync, cancellationToken).ConfigureAwait(false);
        var before = await source.MeasureAsync(cancellationToken).ConfigureAwait(false);
        await source.ValidateAsync(publication.ValidateSourceAsync, cancellationToken).ConfigureAwait(false);
        var validated = await source.MeasureAsync(cancellationToken).ConfigureAwait(false);
        before.EnsureMatches(
            validated,
            publication.Paths.TemporaryFile.Target,
            "Artifact bytes changed during feature validation");
        ArtifactFileCreateOnlyMove.Move(
            publication.Paths,
            source,
            cancellationToken);
        return await publication
            .CreateReferenceAsync(source, validated, cancellationToken)
            .ConfigureAwait(false);
    }
}
