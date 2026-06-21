using System;

#nullable enable

namespace MackySoft.Ucli.Unity
{
    /// <summary> Represents summary values returned by a uCLI executeMethod build runner. </summary>
    public sealed class UcliBuildRunnerSummary
    {
        /// <summary> Initializes a new instance of the <see cref="UcliBuildRunnerSummary" /> class. </summary>
        /// <param name="durationMilliseconds"> The runner invocation duration in milliseconds. </param>
        /// <param name="errorCount"> The runner-observed error count. </param>
        /// <param name="warningCount"> The runner-observed warning count. </param>
        public UcliBuildRunnerSummary (
            long durationMilliseconds,
            int errorCount,
            int warningCount)
        {
            if (durationMilliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(durationMilliseconds), durationMilliseconds, "durationMilliseconds must be non-negative.");
            }

            if (errorCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(errorCount), errorCount, "errorCount must be non-negative.");
            }

            if (warningCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(warningCount), warningCount, "warningCount must be non-negative.");
            }

            DurationMilliseconds = durationMilliseconds;
            ErrorCount = errorCount;
            WarningCount = warningCount;
        }

        /// <summary> Gets the runner invocation duration in milliseconds. </summary>
        public long DurationMilliseconds { get; }

        /// <summary> Gets the runner-observed error count. </summary>
        public int ErrorCount { get; }

        /// <summary> Gets the runner-observed warning count. </summary>
        public int WarningCount { get; }
    }
}
