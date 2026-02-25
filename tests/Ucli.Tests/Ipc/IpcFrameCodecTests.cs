using System.Buffers.Binary;
using System.Text;
using MackySoft.Ucli.Ipc;

namespace MackySoft.Ucli.Tests.Ipc;

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

    private sealed record TestEnvelope (
        string Message,
        int Count);
}
