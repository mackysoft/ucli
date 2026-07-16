using System;
using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.Staging
{
    /// <summary> Writes normalized screenshot bytes to project-scoped staging storage. </summary>
    internal interface IScreenshotStagingImageWriter
    {
        /// <summary> Writes one staging image atomically without replacing an existing target. </summary>
        Task<long> WriteAtomicAsync (
            Guid captureId,
            ReadOnlyMemory<byte> bytes,
            CancellationToken cancellationToken);

        /// <summary> Deletes a staging image when present. </summary>
        void DeleteIfExists (Guid captureId);
    }
}
