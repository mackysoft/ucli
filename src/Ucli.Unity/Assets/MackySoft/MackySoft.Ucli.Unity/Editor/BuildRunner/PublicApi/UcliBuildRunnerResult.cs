using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

#nullable enable

namespace MackySoft.Ucli.Unity
{
    /// <summary> Represents the terminal result returned by a uCLI executeMethod build runner. </summary>
    public sealed class UcliBuildRunnerResult
    {
        /// <summary> Initializes a new instance of the <see cref="UcliBuildRunnerResult" /> class. </summary>
        /// <param name="status"> The terminal status literal: <c>succeeded</c>, <c>failed</c>, or <c>canceled</c>. </param>
        /// <param name="outputs"> The runner-declared output paths relative to <see cref="UcliBuildRunnerContext.OutputDir" />. </param>
        /// <param name="summary"> The runner terminal summary. </param>
        /// <param name="diagnostics"> The runner diagnostics. </param>
        /// <param name="buildReport"> The optional BuildReport JSON source file relative to <see cref="UcliBuildRunnerContext.OutputDir" />. </param>
        public UcliBuildRunnerResult (
            string status,
            IReadOnlyList<string>? outputs,
            UcliBuildRunnerSummary summary,
            IReadOnlyList<UcliBuildRunnerDiagnostic>? diagnostics = null,
            UcliBuildRunnerBuildReport? buildReport = null)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                throw new ArgumentException("status must not be empty.", nameof(status));
            }

            Status = status;
            Outputs = outputs ?? Array.Empty<string>();
            Summary = summary ?? throw new ArgumentNullException(nameof(summary));
            Diagnostics = diagnostics ?? Array.Empty<UcliBuildRunnerDiagnostic>();
            BuildReport = buildReport;
        }

        /// <summary> Gets the terminal status literal. </summary>
        public string Status { get; }

        /// <summary> Gets the runner-declared output paths. </summary>
        public IReadOnlyList<string> Outputs { get; }

        /// <summary> Gets the runner terminal summary. </summary>
        public UcliBuildRunnerSummary Summary { get; }

        /// <summary> Gets the runner diagnostics. </summary>
        public IReadOnlyList<UcliBuildRunnerDiagnostic> Diagnostics { get; }

        /// <summary> Gets optional BuildReport evidence source declared by the runner. </summary>
        public UcliBuildRunnerBuildReport? BuildReport { get; }

        /// <summary> Creates a successful runner result. </summary>
        public static UcliBuildRunnerResult Succeeded (
            IReadOnlyList<string> outputs,
            long durationMilliseconds = 0,
            int warningCount = 0,
            IReadOnlyList<UcliBuildRunnerDiagnostic>? diagnostics = null,
            UcliBuildRunnerBuildReport? buildReport = null)
        {
            return new UcliBuildRunnerResult(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                outputs,
                new UcliBuildRunnerSummary(durationMilliseconds, errorCount: 0, warningCount),
                diagnostics,
                buildReport);
        }

        /// <summary> Creates a failed runner result. </summary>
        public static UcliBuildRunnerResult Failed (
            long durationMilliseconds = 0,
            int errorCount = 1,
            int warningCount = 0,
            IReadOnlyList<string>? outputs = null,
            IReadOnlyList<UcliBuildRunnerDiagnostic>? diagnostics = null,
            UcliBuildRunnerBuildReport? buildReport = null)
        {
            return new UcliBuildRunnerResult(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Failed),
                outputs,
                new UcliBuildRunnerSummary(durationMilliseconds, errorCount, warningCount),
                diagnostics,
                buildReport);
        }

        /// <summary> Creates a canceled runner result. </summary>
        public static UcliBuildRunnerResult Canceled (
            long durationMilliseconds = 0,
            int warningCount = 0,
            IReadOnlyList<string>? outputs = null,
            IReadOnlyList<UcliBuildRunnerDiagnostic>? diagnostics = null,
            UcliBuildRunnerBuildReport? buildReport = null)
        {
            return new UcliBuildRunnerResult(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Canceled),
                outputs,
                new UcliBuildRunnerSummary(durationMilliseconds, errorCount: 0, warningCount),
                diagnostics,
                buildReport);
        }
    }
}
