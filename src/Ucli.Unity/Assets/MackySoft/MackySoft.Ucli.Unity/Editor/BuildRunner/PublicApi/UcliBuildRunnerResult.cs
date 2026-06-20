using System;
using System.Collections.Generic;

#nullable enable

namespace MackySoft.Ucli.Unity
{
    /// <summary> Represents the terminal result returned by a uCLI executeMethod build runner. </summary>
    public sealed class UcliBuildRunnerResult
    {
        /// <summary> Initializes a new instance of the <see cref="UcliBuildRunnerResult" /> class. </summary>
        /// <param name="status"> The terminal status literal: <c>succeeded</c>, <c>failed</c>, or <c>canceled</c>. </param>
        /// <param name="durationMilliseconds"> The runner invocation duration in milliseconds. </param>
        /// <param name="errorCount"> The runner-observed error count. </param>
        /// <param name="warningCount"> The runner-observed warning count. </param>
        /// <param name="outputs"> The runner-declared output paths relative to <see cref="UcliBuildRunnerContext.OutputDir" />. </param>
        public UcliBuildRunnerResult (
            string status,
            long durationMilliseconds,
            int errorCount,
            int warningCount,
            IReadOnlyList<string>? outputs = null)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                throw new ArgumentException("status must not be empty.", nameof(status));
            }

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

            Status = status;
            DurationMilliseconds = durationMilliseconds;
            ErrorCount = errorCount;
            WarningCount = warningCount;
            Outputs = outputs ?? Array.Empty<string>();
        }

        /// <summary> Gets the terminal status literal. </summary>
        public string Status { get; }

        /// <summary> Gets the runner-declared output paths. </summary>
        public IReadOnlyList<string> Outputs { get; }

        /// <summary> Gets the runner invocation duration in milliseconds. </summary>
        public long DurationMilliseconds { get; }

        /// <summary> Gets the runner-observed error count. </summary>
        public int ErrorCount { get; }

        /// <summary> Gets the runner-observed warning count. </summary>
        public int WarningCount { get; }

        /// <summary> Creates a successful runner result. </summary>
        public static UcliBuildRunnerResult Succeeded (
            long durationMilliseconds = 0,
            int warningCount = 0,
            IReadOnlyList<string>? outputs = null)
        {
            return new UcliBuildRunnerResult("succeeded", durationMilliseconds, errorCount: 0, warningCount, outputs);
        }

        /// <summary> Creates a failed runner result. </summary>
        public static UcliBuildRunnerResult Failed (
            long durationMilliseconds = 0,
            int errorCount = 1,
            int warningCount = 0,
            IReadOnlyList<string>? outputs = null)
        {
            return new UcliBuildRunnerResult("failed", durationMilliseconds, errorCount, warningCount, outputs);
        }

        /// <summary> Creates a canceled runner result. </summary>
        public static UcliBuildRunnerResult Canceled (
            long durationMilliseconds = 0,
            int warningCount = 0,
            IReadOnlyList<string>? outputs = null)
        {
            return new UcliBuildRunnerResult("canceled", durationMilliseconds, errorCount: 0, warningCount, outputs);
        }
    }
}
