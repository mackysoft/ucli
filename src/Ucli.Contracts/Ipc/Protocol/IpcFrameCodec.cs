using System.Buffers.Binary;
using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Encodes and decodes IPC frames using <c>length-prefix + UTF-8 JSON</c>. </summary>
public static class IpcFrameCodec
{
    /// <summary> Gets the default maximum frame size in bytes. </summary>
    public const int DefaultMaxFrameSizeInBytes = 16 * 1024 * 1024;

    /// <summary> Writes one model instance as a length-prefixed JSON frame. </summary>
    /// <typeparam name="T"> The model type to serialize. </typeparam>
    /// <param name="stream"> The destination stream. </param>
    /// <param name="value"> The model value to write. </param>
    /// <param name="serializerOptions"> The serializer options used for JSON writing. </param>
    /// <param name="maxFrameSizeInBytes"> The maximum permitted payload size. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes after all bytes are flushed. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="stream" />, <paramref name="value" />, or <paramref name="serializerOptions" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="maxFrameSizeInBytes" /> is less than 1. </exception>
    /// <exception cref="InvalidDataException"> Thrown when payload size exceeds <paramref name="maxFrameSizeInBytes" />. </exception>
    public static async ValueTask WriteModelAsync<T> (
        Stream stream,
        T value,
        JsonSerializerOptions serializerOptions,
        int maxFrameSizeInBytes = DefaultMaxFrameSizeInBytes,
        CancellationToken cancellationToken = default)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (serializerOptions == null)
        {
            throw new ArgumentNullException(nameof(serializerOptions));
        }

        cancellationToken.ThrowIfCancellationRequested();
        ValidateMaxFrameSize(maxFrameSizeInBytes);

        var payload = JsonSerializer.SerializeToUtf8Bytes(value, serializerOptions);
        if (payload.Length > maxFrameSizeInBytes)
        {
            throw new InvalidDataException($"IPC payload exceeds maximum frame size: {payload.Length} > {maxFrameSizeInBytes}.");
        }

        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);

        await stream.WriteAsync(header.AsMemory(), cancellationToken);
        await stream.WriteAsync(payload.AsMemory(), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    /// <summary> Reads one length-prefixed JSON frame and deserializes it into the target model type. </summary>
    /// <typeparam name="T"> The model type to deserialize. </typeparam>
    /// <param name="stream"> The source stream. </param>
    /// <param name="serializerOptions"> The serializer options used for JSON reading. </param>
    /// <param name="maxFrameSizeInBytes"> The maximum permitted payload size. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The deserialized model instance. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="stream" /> or <paramref name="serializerOptions" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="maxFrameSizeInBytes" /> is less than 1. </exception>
    /// <exception cref="InvalidDataException"> Thrown when frame format is invalid or JSON cannot be deserialized. </exception>
    /// <exception cref="EndOfStreamException"> Thrown when frame bytes are truncated. </exception>
    public static async ValueTask<T> ReadModelAsync<T> (
        Stream stream,
        JsonSerializerOptions serializerOptions,
        int maxFrameSizeInBytes = DefaultMaxFrameSizeInBytes,
        CancellationToken cancellationToken = default)
    {
        var readResult = await TryReadModelAsync<T>(
                stream,
                serializerOptions,
                maxFrameSizeInBytes,
                cancellationToken)
            .ConfigureAwait(false);
        if (readResult.IsSuccess)
        {
            return readResult.Value;
        }

        throw CreateReadModelException(readResult.ErrorKind, readResult.ErrorMessage);
    }

    /// <summary> Tries to read one length-prefixed JSON frame and deserialize it to the target model type. </summary>
    /// <typeparam name="T"> The model type to deserialize. </typeparam>
    /// <param name="stream"> The source stream. </param>
    /// <param name="serializerOptions"> The serializer options used for JSON reading. </param>
    /// <param name="maxFrameSizeInBytes"> The maximum permitted payload size. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The frame read result that contains either deserialized model value or one machine-readable error kind. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="stream" /> or <paramref name="serializerOptions" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="maxFrameSizeInBytes" /> is less than 1. </exception>
    public static async ValueTask<IpcFrameReadResult<T>> TryReadModelAsync<T> (
        Stream stream,
        JsonSerializerOptions serializerOptions,
        int maxFrameSizeInBytes = DefaultMaxFrameSizeInBytes,
        CancellationToken cancellationToken = default)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (serializerOptions == null)
        {
            throw new ArgumentNullException(nameof(serializerOptions));
        }

        cancellationToken.ThrowIfCancellationRequested();
        ValidateMaxFrameSize(maxFrameSizeInBytes);

        var header = new byte[sizeof(int)];
        try
        {
            await ReadExactlyAsync(stream, header.AsMemory(), cancellationToken);
        }
        catch (EndOfStreamException exception)
        {
            return IpcFrameReadResult<T>.Failure(IpcFrameReadErrorKind.HeaderTruncated, exception.Message);
        }
        catch (Exception exception) when (IsStreamReadFailure(exception))
        {
            return IpcFrameReadResult<T>.Failure(IpcFrameReadErrorKind.StreamReadFailed, exception.Message);
        }

        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (payloadLength < 0)
        {
            return IpcFrameReadResult<T>.Failure(
                IpcFrameReadErrorKind.PayloadLengthNegative,
                $"IPC payload length must be non-negative. Actual: {payloadLength}.");
        }

        if (payloadLength > maxFrameSizeInBytes)
        {
            return IpcFrameReadResult<T>.Failure(
                IpcFrameReadErrorKind.PayloadTooLarge,
                $"IPC payload exceeds maximum frame size: {payloadLength} > {maxFrameSizeInBytes}.");
        }

        var payload = new byte[payloadLength];
        try
        {
            await ReadExactlyAsync(stream, payload.AsMemory(), cancellationToken);
        }
        catch (EndOfStreamException exception)
        {
            return IpcFrameReadResult<T>.Failure(IpcFrameReadErrorKind.PayloadTruncated, exception.Message);
        }
        catch (Exception exception) when (IsStreamReadFailure(exception))
        {
            return IpcFrameReadResult<T>.Failure(IpcFrameReadErrorKind.StreamReadFailed, exception.Message);
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(payload, serializerOptions);
            if (value is null)
            {
                return IpcFrameReadResult<T>.Failure(
                    IpcFrameReadErrorKind.PayloadModelNull,
                    "IPC payload could not be deserialized into the target model.");
            }

            return IpcFrameReadResult<T>.Success(value);
        }
        catch (JsonException exception)
        {
            return IpcFrameReadResult<T>.Failure(IpcFrameReadErrorKind.PayloadJsonInvalid, exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return IpcFrameReadResult<T>.Failure(IpcFrameReadErrorKind.PayloadJsonInvalid, exception.Message);
        }
    }

    /// <summary> Reads exactly the specified number of bytes from the source stream. </summary>
    /// <param name="stream"> The source stream. </param>
    /// <param name="buffer"> The destination buffer. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes after the buffer is fully filled. </returns>
    /// <exception cref="EndOfStreamException"> Thrown when stream ends before the buffer is fully read. </exception>
    private static async ValueTask ReadExactlyAsync (
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var readLength = await stream.ReadAsync(buffer[offset..], cancellationToken);
            if (readLength == 0)
            {
                throw new EndOfStreamException("IPC stream ended before a complete frame was read.");
            }

            offset += readLength;
        }
    }

    /// <summary> Validates the configured maximum frame size. </summary>
    /// <param name="maxFrameSizeInBytes"> The maximum frame size to validate. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when the value is less than 1. </exception>
    private static void ValidateMaxFrameSize (int maxFrameSizeInBytes)
    {
        if (maxFrameSizeInBytes < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxFrameSizeInBytes),
                maxFrameSizeInBytes,
                "Maximum frame size must be greater than zero.");
        }
    }

    /// <summary> Determines whether one exception indicates stream read failure in transport boundary. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when exception indicates stream read failure; otherwise <see langword="false" />. </returns>
    private static bool IsStreamReadFailure (Exception exception)
    {
        return exception is IOException
            or InvalidDataException
            or ObjectDisposedException
            or InvalidOperationException
            or NotSupportedException;
    }

    /// <summary> Creates one legacy exception from frame read error kind for <see cref="ReadModelAsync{T}" /> compatibility. </summary>
    /// <param name="errorKind"> The frame read error kind. </param>
    /// <param name="errorMessage"> The diagnostic frame read error message. </param>
    /// <returns> The mapped exception instance. </returns>
    private static Exception CreateReadModelException (
        IpcFrameReadErrorKind errorKind,
        string errorMessage)
    {
        return errorKind switch
        {
            IpcFrameReadErrorKind.HeaderTruncated => new EndOfStreamException(errorMessage),
            IpcFrameReadErrorKind.PayloadTruncated => new EndOfStreamException(errorMessage),
            IpcFrameReadErrorKind.PayloadJsonInvalid => new InvalidDataException("IPC payload contains invalid JSON."),
            IpcFrameReadErrorKind.PayloadLengthNegative => new InvalidDataException(errorMessage),
            IpcFrameReadErrorKind.PayloadTooLarge => new InvalidDataException(errorMessage),
            IpcFrameReadErrorKind.PayloadModelNull => new InvalidDataException(errorMessage),
            IpcFrameReadErrorKind.StreamReadFailed => new InvalidDataException(errorMessage),
            _ => new InvalidDataException(errorMessage),
        };
    }
}