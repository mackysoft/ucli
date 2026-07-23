using System;
using System.Collections.Generic;
using MackySoft.Text.Vocabularies;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

#nullable enable

namespace MackySoft.Ucli.Unity
{
    /// <summary> Represents the terminal result returned by a uCLI executeMethod build runner. </summary>
    public sealed class UcliBuildRunnerResult
    {
        /// <summary> Initializes a new instance of the <see cref="UcliBuildRunnerResult" /> class. </summary>
        /// <param name="status"> The terminal build result. </param>
        /// <param name="outputs"> The runner-declared output paths relative to <see cref="UcliBuildRunnerContext.OutputDir" />. </param>
        /// <param name="summary"> The runner terminal summary. </param>
        /// <param name="diagnostics"> The runner diagnostics. </param>
        /// <param name="buildReport"> The optional BuildReport JSON source file relative to <see cref="UcliBuildRunnerContext.OutputDir" />. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="summary" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when a collection contains a <see langword="null" /> item, or when a successful result declares no outputs. </exception>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="status" /> is not a terminal build result. </exception>
        public UcliBuildRunnerResult (
            IpcBuildReportResult status,
            IReadOnlyList<string>? outputs,
            UcliBuildRunnerSummary summary,
            IReadOnlyList<UcliBuildRunnerDiagnostic>? diagnostics,
            UcliBuildRunnerBuildReport? buildReport)
        {
            if (!TextVocabulary.IsDefined(status) || status == IpcBuildReportResult.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(status), status, "status must be terminal.");
            }

            var runnerSummary = summary ?? throw new ArgumentNullException(nameof(summary));
            var outputSnapshot = CreateSnapshot(outputs, nameof(outputs));
            var diagnosticSnapshot = CreateSnapshot(diagnostics, nameof(diagnostics));
            if (status == IpcBuildReportResult.Succeeded && outputSnapshot.Count == 0)
            {
                throw new ArgumentException("A successful build runner result must declare at least one output.", nameof(outputs));
            }

            Status = status;
            Outputs = outputSnapshot;
            Summary = runnerSummary;
            Diagnostics = diagnosticSnapshot;
            BuildReport = buildReport;
        }

        /// <summary> Gets the terminal status literal. </summary>
        public IpcBuildReportResult Status { get; }

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
                IpcBuildReportResult.Succeeded,
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
                IpcBuildReportResult.Failed,
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
                IpcBuildReportResult.Canceled,
                outputs,
                new UcliBuildRunnerSummary(durationMilliseconds, errorCount: 0, warningCount),
                diagnostics,
                buildReport);
        }

        private static IReadOnlyList<T> CreateSnapshot<T> (
            IReadOnlyList<T>? values,
            string parameterName)
            where T : class
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<T>();
            }

            var snapshot = new T[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                snapshot[index] = values[index]
                    ?? throw new ArgumentException($"Collection item at index {index} must not be null.", parameterName);
            }

            return Array.AsReadOnly(snapshot);
        }
    }
}
