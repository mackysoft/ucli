namespace MackySoft.Ucli.Unity.ScreenshotCapture
{
    /// <summary> Cleans request-owned temporary GameView resolutions when a resolution transaction requires it. </summary>
    internal interface IUnityScreenshotResolutionOrphanCleaner
    {
        /// <summary> Removes only temporary entries whose ownership and non-interference are proven. </summary>
        bool TryCleanup (out string errorMessage);
    }
}
