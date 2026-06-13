using System.Globalization;
using System.Security.Cryptography;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Creates collision-resistant build run identifiers. </summary>
internal sealed class BuildRunIdFactory : IBuildRunIdFactory
{
    private const string RunIdTimestampFormat = "yyyyMMdd_HHmmss'Z'";

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="BuildRunIdFactory" /> class. </summary>
    public BuildRunIdFactory (TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string Create ()
    {
        var utcNow = timeProvider.GetUtcNow();
        var suffix = RandomNumberGenerator.GetHexString(8).ToLowerInvariant();
        return $"{utcNow.ToString(RunIdTimestampFormat, CultureInfo.InvariantCulture)}_{suffix}";
    }
}
