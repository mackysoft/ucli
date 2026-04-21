using System.Buffers.Binary;
using System.Text;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcFrameCodecContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteModelAsync_And_ReadModelAsync_RoundTripsPayload ()
    {
        var source = new TestEnvelope("hello", 42);
        await using var stream = new MemoryStream();

        await IpcFrameCodec.WriteModelAsync(stream, source, IpcJsonSerializerOptions.Default);
        stream.Position = 0;

        var actual = await IpcFrameCodec.ReadModelAsync<TestEnvelope>(stream, IpcJsonSerializerOptions.Default);

        Assert.Equal(source, actual);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryReadModelAsync_WithValidFrame_ReturnsSuccess ()
    {
        var source = new TestEnvelope("hello", 42);
        await using var stream = new MemoryStream();

        await IpcFrameCodec.WriteModelAsync(stream, source, IpcJsonSerializerOptions.Default);
        stream.Position = 0;

        var result = await IpcFrameCodec.TryReadModelAsync<TestEnvelope>(stream, IpcJsonSerializerOptions.Default);

        Assert.True(result.IsSuccess);
        Assert.Equal(IpcFrameReadErrorKind.None, result.ErrorKind);
        Assert.Equal(source, result.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadModelAsync_WithNegativeLength_ThrowsInvalidDataException ()
    {
        await using var stream = new MemoryStream();
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, -1);
        await stream.WriteAsync(header);
        stream.Position = 0;

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await IpcFrameCodec.ReadModelAsync<TestEnvelope>(stream, IpcJsonSerializerOptions.Default);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryReadModelAsync_WithNegativeLength_ReturnsPayloadLengthNegative ()
    {
        await using var stream = new MemoryStream();
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, -1);
        await stream.WriteAsync(header);
        stream.Position = 0;

        var result = await IpcFrameCodec.TryReadModelAsync<TestEnvelope>(stream, IpcJsonSerializerOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcFrameReadErrorKind.PayloadLengthNegative, result.ErrorKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadModelAsync_WithTruncatedPayload_ThrowsEndOfStreamException ()
    {
        await using var stream = new MemoryStream();
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, 10);
        await stream.WriteAsync(header);
        await stream.WriteAsync(new byte[] { 1, 2, 3 });
        stream.Position = 0;

        await Assert.ThrowsAsync<EndOfStreamException>(async () =>
        {
            await IpcFrameCodec.ReadModelAsync<TestEnvelope>(stream, IpcJsonSerializerOptions.Default);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryReadModelAsync_WithTruncatedHeader_ReturnsHeaderTruncated ()
    {
        await using var stream = new MemoryStream();
        await stream.WriteAsync(new byte[] { 1, 2, 3 });
        stream.Position = 0;

        var result = await IpcFrameCodec.TryReadModelAsync<TestEnvelope>(stream, IpcJsonSerializerOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcFrameReadErrorKind.HeaderTruncated, result.ErrorKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryReadModelAsync_WithTruncatedPayload_ReturnsPayloadTruncated ()
    {
        await using var stream = new MemoryStream();
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, 10);
        await stream.WriteAsync(header);
        await stream.WriteAsync(new byte[] { 1, 2, 3 });
        stream.Position = 0;

        var result = await IpcFrameCodec.TryReadModelAsync<TestEnvelope>(stream, IpcJsonSerializerOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcFrameReadErrorKind.PayloadTruncated, result.ErrorKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryReadModelAsync_WhenReadThrowsInvalidData_ReturnsStreamReadFailed ()
    {
        await using var stream = new ThrowingReadStream(new InvalidDataException("read failed"));

        var result = await IpcFrameCodec.TryReadModelAsync<TestEnvelope>(stream, IpcJsonSerializerOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcFrameReadErrorKind.StreamReadFailed, result.ErrorKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadModelAsync_WithInvalidJson_ThrowsInvalidDataException ()
    {
        await using var stream = new MemoryStream();
        var payload = Encoding.UTF8.GetBytes("not-json");
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header);
        await stream.WriteAsync(payload);
        stream.Position = 0;

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await IpcFrameCodec.ReadModelAsync<TestEnvelope>(stream, IpcJsonSerializerOptions.Default);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryReadModelAsync_WithInvalidJson_ReturnsPayloadJsonInvalid ()
    {
        await using var stream = new MemoryStream();
        var payload = Encoding.UTF8.GetBytes("not-json");
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header);
        await stream.WriteAsync(payload);
        stream.Position = 0;

        var result = await IpcFrameCodec.TryReadModelAsync<TestEnvelope>(stream, IpcJsonSerializerOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcFrameReadErrorKind.PayloadJsonInvalid, result.ErrorKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteModelAsync_WithOversizedPayload_ThrowsInvalidDataException ()
    {
        await using var stream = new MemoryStream();
        var source = new TestEnvelope(new string('a', 2048), 1);

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await IpcFrameCodec.WriteModelAsync(
                stream,
                source,
                IpcJsonSerializerOptions.Default,
                maxFrameSizeInBytes: 32);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryReadModelAsync_WithOversizedLength_ReturnsPayloadTooLarge ()
    {
        await using var stream = new MemoryStream();
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, 4096);
        await stream.WriteAsync(header);
        stream.Position = 0;

        var result = await IpcFrameCodec.TryReadModelAsync<TestEnvelope>(
            stream,
            IpcJsonSerializerOptions.Default,
            maxFrameSizeInBytes: 32);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcFrameReadErrorKind.PayloadTooLarge, result.ErrorKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryReadModelAsync_WithJsonNullPayload_ReturnsPayloadModelNull ()
    {
        await using var stream = new MemoryStream();
        var payload = Encoding.UTF8.GetBytes("null");
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header);
        await stream.WriteAsync(payload);
        stream.Position = 0;

        var result = await IpcFrameCodec.TryReadModelAsync<TestEnvelope>(stream, IpcJsonSerializerOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcFrameReadErrorKind.PayloadModelNull, result.ErrorKind);
    }

    private sealed record TestEnvelope (
        string Message,
        int Count);

    private sealed class ThrowingReadStream : Stream
    {
        private readonly Exception exception;

        public ThrowingReadStream (Exception exception)
        {
            this.exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush ()
        {
            throw new NotSupportedException();
        }

        public override int Read (
            byte[] buffer,
            int offset,
            int count)
        {
            throw exception;
        }

        public override Task<int> ReadAsync (
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            return Task.FromException<int>(exception);
        }

        public override ValueTask<int> ReadAsync (
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<int>(exception);
        }

        public override long Seek (
            long offset,
            SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength (long value)
        {
            throw new NotSupportedException();
        }

        public override void Write (
            byte[] buffer,
            int offset,
            int count)
        {
            throw new NotSupportedException();
        }
    }
}