using System.Buffers;
using System.Security.Cryptography;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Infrastructure.Artifacts;

/// <summary> Computes an artifact digest and byte count from one borrowed seekable stream. </summary>
internal static class ArtifactFileMeasurementReader
{
    private const int FileReadBufferSize = 4096;

    public static async ValueTask<ImmutableArtifactFileReadBoundary.Measurement> MeasureAsync (
        Stream stream,
        CancellationToken cancellationToken)
    {
        stream.Position = 0;
        var buffer = ArrayPool<byte>.Shared.Rent(FileReadBufferSize);
        try
        {
            return await ReadToEndAsync(stream, buffer, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async ValueTask<ImmutableArtifactFileReadBoundary.Measurement> ReadToEndAsync (
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        ulong sizeBytes = 0;
        while (true)
        {
            var readCount = await stream
                .ReadAsync(buffer.AsMemory(0, FileReadBufferSize), cancellationToken)
                .ConfigureAwait(false);
            if (readCount == 0)
            {
                return new ImmutableArtifactFileReadBoundary.Measurement(
                    Sha256LowerHex.GetHashAndReset(hash),
                    sizeBytes);
            }

            hash.AppendData(buffer, 0, readCount);
            sizeBytes = checked(sizeBytes + (uint)readCount);
        }
    }
}
