namespace MackySoft.Ucli.Application.Features.Screenshot.Capture;

/// <summary> Creates timestamped collision-resistant screenshot capture identifiers. </summary>
internal sealed class ScreenshotCaptureIdFactory : IScreenshotCaptureIdFactory
{
    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="ScreenshotCaptureIdFactory" /> class. </summary>
    public ScreenshotCaptureIdFactory (TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public string Create ()
    {
        return TimestampedExecutionId.Create(timeProvider.GetUtcNow());
    }
}
