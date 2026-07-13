namespace MackySoft.Ucli.Features.Screenshot.Artifacts.Png;

/// <summary> Computes the ISO 3309 CRC used by PNG chunks. </summary>
internal static class PngCrc32
{
    private const uint Polynomial = 0xEDB88320;
    private static readonly uint[] Table = CreateTable();

    public static uint Start () => uint.MaxValue;

    public static uint Append (
        uint crc,
        ReadOnlySpan<byte> bytes)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            crc = Table[(crc ^ bytes[i]) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    public static uint Finish (uint crc) => crc ^ uint.MaxValue;

    private static uint[] CreateTable ()
    {
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0
                    ? Polynomial ^ (value >> 1)
                    : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }
}
