namespace MackySoft.Ucli.Application.Features.Programs.Persistence;

/// <summary>Publishes and reads immutable artifacts that belong to one Program Run.</summary>
internal interface IProgramArtifactStore
{
    ValueTask<ArtifactRef> PublishAsync (
        Guid runId,
        ArtifactKind kind,
        ArtifactMediaType mediaType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default);

    ValueTask<byte[]?> ReadAsync (ArtifactRef artifact, CancellationToken cancellationToken = default);
}

/// <summary>Opens the immutable artifact namespace for a fixed Program project.</summary>
internal interface IProgramArtifactStoreFactory
{
    IProgramArtifactStore ForProject (MackySoft.Ucli.Application.Shared.Context.Project.ResolvedUnityProjectContext project);
}
