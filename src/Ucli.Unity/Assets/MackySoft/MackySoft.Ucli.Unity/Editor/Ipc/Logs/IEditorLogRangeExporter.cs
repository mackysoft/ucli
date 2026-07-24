using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.FileSystem;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Exports one byte-range from Unity editor log into destination artifact path. </summary>
    internal interface IEditorLogRangeExporter
    {
        /// <summary> Exports one half-open byte range from source log file into destination file. </summary>
        /// <param name="sourcePath"> The source log file path. </param>
        /// <param name="destinationPath"> The destination artifact file path. </param>
        /// <param name="startOffset"> The inclusive start byte offset. </param>
        /// <param name="endOffset"> The exclusive end byte offset. </param>
        /// <param name="redactionValues"> The sensitive values to redact while writing the artifact. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
        /// <returns> The counters collected from the exported log range. </returns>
        Task<EditorLogRangeExportResult> ExportRangeAsync (
            AbsolutePath sourcePath,
            AbsolutePath destinationPath,
            long startOffset,
            long endOffset,
            IEnumerable<string>? redactionValues = null,
            CancellationToken cancellationToken = default);
    }
}
