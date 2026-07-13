namespace MackySoft.Ucli.Application.Features.Screenshot.Capture;

/// <summary> Creates collision-resistant screenshot capture identifiers. </summary>
internal interface IScreenshotCaptureIdFactory
{
    /// <summary> Creates one screenshot capture identifier. </summary>
    string Create ();
}
