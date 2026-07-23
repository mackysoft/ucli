using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Infrastructure.Index;

/// <summary> Computes file-content hashes while treating transient filesystem misses as unavailable input. </summary>
internal static class FileContentHash
{
    /// <summary> Tries to compute one SHA-256 lower-hex hash for an existing file. </summary>
    /// <param name="filePath"> The file path to hash. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The computed hash when the file can be read; otherwise <see langword="null" />. </returns>
    public static async ValueTask<Sha256Digest?> TryComputeFileHashAsync (
        AbsolutePath filePath,
        CancellationToken cancellationToken = default)
    {
        if (filePath is null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(filePath.Value))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(filePath.Value, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            return null;
        }

        return Sha256Digest.Compute(bytes);
    }
}
