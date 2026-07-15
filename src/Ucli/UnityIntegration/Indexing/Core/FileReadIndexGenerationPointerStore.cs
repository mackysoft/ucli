using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Core;

/// <summary> Stores the current read-index generation as one atomically replaced canonical GUID. </summary>
internal sealed class FileReadIndexGenerationPointerStore : IReadIndexGenerationPointerStore
{
    /// <inheritdoc />
    public async ValueTask<Guid?> ReadAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken)
    {
        var pointerPath = UcliStoragePathResolver.ResolveReadIndexCurrentGenerationPath(
            storageRoot,
            projectFingerprint);
        var value = await FileUtilities.ReadAllTextOrNullAsync(pointerPath, cancellationToken).ConfigureAwait(false);
        if (value == null)
        {
            return null;
        }

        if (!Guid.TryParseExact(value, "N", out var generationId)
            || generationId == Guid.Empty
            || !string.Equals(value, generationId.ToString("N"), StringComparison.Ordinal))
        {
            throw new InvalidDataException("The current read-index generation pointer is malformed.");
        }

        return generationId;
    }

    /// <inheritdoc />
    public ValueTask PublishAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        Guid generationId,
        CancellationToken cancellationToken)
    {
        if (generationId == Guid.Empty)
        {
            throw new ArgumentException("Generation identifier must not be empty.", nameof(generationId));
        }

        return FileUtilities.WriteAllTextAtomicallyAsync(
            UcliStoragePathResolver.ResolveReadIndexCurrentGenerationPath(storageRoot, projectFingerprint),
            generationId.ToString("N"),
            cancellationToken);
    }
}
