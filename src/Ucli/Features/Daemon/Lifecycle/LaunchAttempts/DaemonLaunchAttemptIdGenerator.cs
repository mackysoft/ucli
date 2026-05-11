using System.Globalization;
using System.Security.Cryptography;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.LaunchAttempts;

/// <summary> Generates timestamp-prefixed daemon launch-attempt identifiers. </summary>
internal sealed class DaemonLaunchAttemptIdGenerator : IDaemonLaunchAttemptIdGenerator
{
    private const string TimestampFormat = "yyyyMMdd_HHmmss'Z'";

    /// <inheritdoc />
    public string Create (DateTimeOffset startedAtUtc)
    {
        var suffix = RandomNumberGenerator.GetHexString(8).ToLowerInvariant();
        return $"{startedAtUtc.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture)}_{suffix}";
    }
}
