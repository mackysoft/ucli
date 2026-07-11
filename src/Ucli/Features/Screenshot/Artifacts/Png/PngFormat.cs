namespace MackySoft.Ucli.Features.Screenshot.Artifacts.Png;

/// <summary> Defines the PNG signature and chunk type codes owned by the screenshot encoder. </summary>
internal static class PngFormat
{
    public const uint IhdrChunkType = 0x49484452;
    public const uint SrgbChunkType = 0x73524742;
    public const uint IdatChunkType = 0x49444154;
    public const uint IendChunkType = 0x49454E44;

    public static ReadOnlyMemory<byte> Signature { get; } = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
}
