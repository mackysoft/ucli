using System;
using System.Buffers.Binary;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Encodes and decodes IPC frames using <c>length-prefix + UTF-8 JSON</c>. </summary>
    internal static class UnityIpcFrameCodec
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
        /// <exception cref="ArgumentNullException"> Thrown when one reference argument is <see langword="null" />. </exception>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="maxFrameSizeInBytes" /> is less than one. </exception>
        /// <exception cref="InvalidDataException"> Thrown when payload exceeds the configured maximum frame size. </exception>
        public static async ValueTask WriteModel<T> (
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

            await stream.WriteAsync(header, 0, header.Length, cancellationToken);
            await stream.WriteAsync(payload, 0, payload.Length, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        /// <summary> Reads one length-prefixed JSON frame and deserializes it into target model type. </summary>
        /// <typeparam name="T"> The model type to deserialize. </typeparam>
        /// <param name="stream"> The source stream. </param>
        /// <param name="serializerOptions"> The serializer options used for JSON reading. </param>
        /// <param name="maxFrameSizeInBytes"> The maximum permitted payload size. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
        /// <returns> The deserialized model instance. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when one reference argument is <see langword="null" />. </exception>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="maxFrameSizeInBytes" /> is less than one. </exception>
        /// <exception cref="EndOfStreamException"> Thrown when frame bytes are truncated. </exception>
        /// <exception cref="InvalidDataException"> Thrown when payload format is invalid or deserialization fails. </exception>
        public static async ValueTask<T> ReadModel<T> (
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
            await ReadExactly(stream, header, cancellationToken);

            var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header);
            if (payloadLength < 0)
            {
                throw new InvalidDataException($"IPC payload length must be non-negative. Actual: {payloadLength}.");
            }

            if (payloadLength > maxFrameSizeInBytes)
            {
                throw new InvalidDataException($"IPC payload exceeds maximum frame size: {payloadLength} > {maxFrameSizeInBytes}.");
            }

            var payload = new byte[payloadLength];
            await ReadExactly(stream, payload, cancellationToken);

            try
            {
                var value = JsonSerializer.Deserialize<T>(payload, serializerOptions);
                if (value == null)
                {
                    throw new InvalidDataException("IPC payload could not be deserialized into the target model.");
                }

                return value;
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("IPC payload contains invalid JSON.", exception);
            }
        }

        /// <summary> Reads exactly the specified number of bytes from the source stream. </summary>
        /// <param name="stream"> The source stream. </param>
        /// <param name="buffer"> The destination buffer. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
        /// <returns> A task that completes after the buffer is fully filled. </returns>
        /// <exception cref="EndOfStreamException"> Thrown when stream ends before requested bytes are read. </exception>
        private static async Task ReadExactly (
            Stream stream,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var readLength = await stream.ReadAsync(buffer, offset, buffer.Length - offset, cancellationToken);
                if (readLength == 0)
                {
                    throw new EndOfStreamException("IPC stream ended before a complete frame was read.");
                }

                offset += readLength;
            }
        }

        /// <summary> Validates maximum frame-size configuration. </summary>
        /// <param name="maxFrameSizeInBytes"> The maximum frame size to validate. </param>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when value is less than one. </exception>
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
    }
}
