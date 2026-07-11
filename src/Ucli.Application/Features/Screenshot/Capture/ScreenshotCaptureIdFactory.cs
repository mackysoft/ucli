using System.Globalization;
using System.Security.Cryptography;

namespace MackySoft.Ucli.Application.Features.Screenshot.Capture;

/// <summary> Creates timestamped collision-resistant screenshot capture identifiers. </summary>
internal sealed class ScreenshotCaptureIdFactory : IScreenshotCaptureIdFactory
{
    private const string TimestampFormat = "yyyyMMdd_HHmmss'Z'";

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="ScreenshotCaptureIdFactory" /> class. </summary>
    public ScreenshotCaptureIdFactory (TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string Create ()
    {
        var timestamp = timeProvider.GetUtcNow().ToString(TimestampFormat, CultureInfo.InvariantCulture);
        var suffix = RandomNumberGenerator.GetHexString(8).ToLowerInvariant();
        return $"{timestamp}_{suffix}";
    }
}
