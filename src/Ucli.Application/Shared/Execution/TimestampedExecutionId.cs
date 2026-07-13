using System.Globalization;
using System.Security.Cryptography;

namespace MackySoft.Ucli.Application.Shared.Execution;

/// <summary> Creates collision-resistant execution identifiers with a canonical UTC timestamp prefix. </summary>
internal static class TimestampedExecutionId
{
    private const string TimestampFormat = "yyyyMMdd_HHmmss'Z'";

    /// <summary> Creates an identifier from the supplied instant and a random lowercase hexadecimal suffix. </summary>
    /// <param name="timestamp"> The instant represented by the identifier after conversion to UTC. </param>
    /// <returns> An identifier containing a UTC timestamp and eight lowercase hexadecimal characters. </returns>
    public static string Create (DateTimeOffset timestamp)
    {
        var suffix = RandomNumberGenerator.GetHexString(8).ToLowerInvariant();
        return $"{timestamp.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture)}_{suffix}";
    }
}
