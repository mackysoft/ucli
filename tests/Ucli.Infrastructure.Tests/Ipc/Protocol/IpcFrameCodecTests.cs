using System.Buffers.Binary;
using System.Text;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Infrastructure.Tests.Ipc.Protocol;

public sealed class IpcFrameCodecTests
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
    public async Task ReadModelAsync_WhenTransportReadFails_ThrowsIOException ()
    {
        await using var stream = new ThrowingReadStream(new IOException("connection reset"));

        var exception = await Assert.ThrowsAsync<IOException>(async () =>
        {
            await IpcFrameCodec.ReadModelAsync<TestEnvelope>(stream, IpcJsonSerializerOptions.Default);
        });

        Assert.Contains("connection reset", exception.Message, StringComparison.Ordinal);
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

    [Theory]
    [InlineData("""{"protocolVersion":1,"requestId":"req-9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","sessionToken":"token","method":"test.method","payload":{},"responseMode":"single"}""")]
    [InlineData("""{"protocolVersion":1,"requestId":"00000000-0000-0000-0000-000000000000","sessionToken":"token","method":"test.method","payload":{},"responseMode":"single"}""")]
    [InlineData("""{"protocolVersion":1,"sessionToken":"token","method":"test.method","payload":{},"responseMode":"single"}""")]
    [Trait("Size", "Small")]
    public async Task TryReadModelAsync_WithInvalidIpcRequestId_ReturnsPayloadJsonInvalid (string payloadJson)
    {
        await using var stream = new MemoryStream();
        var payload = Encoding.UTF8.GetBytes(payloadJson);
        var header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header);
        await stream.WriteAsync(payload);
        stream.Position = 0;

        var result = await IpcFrameCodec.TryReadModelAsync<IpcRequestEnvelope>(stream, IpcJsonSerializerOptions.Default);

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
}
