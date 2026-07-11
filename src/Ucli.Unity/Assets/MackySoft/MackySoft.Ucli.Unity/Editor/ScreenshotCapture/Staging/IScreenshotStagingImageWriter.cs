using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.Staging
{
    /// <summary> Writes normalized screenshot bytes to a host-owned staging path. </summary>
    internal interface IScreenshotStagingImageWriter
    {
        /// <summary> Writes one staging image atomically without replacing an existing target. </summary>
        Task<long> WriteAtomicAsync (
            string path,
            byte[] bytes,
            CancellationToken cancellationToken);

        /// <summary> Deletes a staging image when present. </summary>
        void DeleteIfExists (string path);
    }
}
