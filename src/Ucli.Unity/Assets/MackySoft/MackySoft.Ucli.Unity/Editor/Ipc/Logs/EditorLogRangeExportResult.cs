namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents counters collected while exporting a byte range from the Unity editor log. </summary>
    internal readonly struct EditorLogRangeExportResult
    {
        /// <summary> Initializes a new instance of the <see cref="EditorLogRangeExportResult"/> struct. </summary>
        /// <param name="entryCount"> The number of non-empty log entries in the exported range. </param>
        /// <param name="errorCount"> The number of exported log entries classified as errors. </param>
        /// <param name="warningCount"> The number of exported log entries classified as warnings. </param>
        public EditorLogRangeExportResult (
            int entryCount,
            int errorCount,
            int warningCount)
        {
            EntryCount = entryCount;
            ErrorCount = errorCount;
            WarningCount = warningCount;
        }

        /// <summary> Gets the number of non-empty log entries in the exported range. </summary>
        public int EntryCount { get; }

        /// <summary> Gets the number of exported log entries classified as errors. </summary>
        public int ErrorCount { get; }

        /// <summary> Gets the number of exported log entries classified as warnings. </summary>
        public int WarningCount { get; }
    }
}
