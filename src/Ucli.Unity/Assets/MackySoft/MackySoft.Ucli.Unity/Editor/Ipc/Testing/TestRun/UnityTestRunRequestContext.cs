using System;
using MackySoft.FileSystem;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents normalized request values required for one Unity test run. </summary>
    internal sealed record UnityTestRunRequestContext
    {
        /// <summary> Initializes the normalized execution context for one identified Unity test run. </summary>
        /// <param name="runId"> The non-empty run identifier used for progress and artifact correlation. </param>
        /// <param name="testPlatform"> The canonical Unity test platform value. </param>
        /// <param name="testMode"> The Unity Test Framework execution mode. </param>
        /// <param name="targetPlatform"> The player build target, or <see langword="null" /> for editor tests. </param>
        /// <param name="testFilter"> The optional test-name filter. </param>
        /// <param name="testCategories"> The validated test-category filters. </param>
        /// <param name="assemblyNames"> The validated assembly-name filters. </param>
        /// <param name="resultsXmlPath"> The host-derived Unity results XML path. </param>
        /// <param name="editorLogPath"> The host-derived editor log path. </param>
        /// <param name="consoleLogPath"> The current Unity Editor console log source path. </param>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="runId" /> is empty. </exception>
        /// <exception cref="ArgumentNullException"> Thrown when a required reference value is <see langword="null" />. </exception>
        public UnityTestRunRequestContext (
            Guid runId,
            string testPlatform,
            TestMode testMode,
            BuildTarget? targetPlatform,
            string? testFilter,
            string[] testCategories,
            string[] assemblyNames,
            AbsolutePath resultsXmlPath,
            AbsolutePath editorLogPath,
            AbsolutePath consoleLogPath)
        {
            if (runId == Guid.Empty)
            {
                throw new ArgumentException("Run id must not be empty.", nameof(runId));
            }

            RunId = runId;
            TestPlatform = testPlatform ?? throw new ArgumentNullException(nameof(testPlatform));
            TestMode = testMode;
            TargetPlatform = targetPlatform;
            TestFilter = testFilter;
            TestCategories = testCategories ?? throw new ArgumentNullException(nameof(testCategories));
            AssemblyNames = assemblyNames ?? throw new ArgumentNullException(nameof(assemblyNames));
            ResultsXmlPath = resultsXmlPath ?? throw new ArgumentNullException(nameof(resultsXmlPath));
            EditorLogPath = editorLogPath ?? throw new ArgumentNullException(nameof(editorLogPath));
            ConsoleLogPath = consoleLogPath ?? throw new ArgumentNullException(nameof(consoleLogPath));
        }

        /// <summary> Gets the non-empty test run identifier. </summary>
        public Guid RunId { get; }

        public string TestPlatform { get; }

        public TestMode TestMode { get; }

        public BuildTarget? TargetPlatform { get; }

        public string? TestFilter { get; }

        public string[] TestCategories { get; }

        public string[] AssemblyNames { get; }

        public AbsolutePath ResultsXmlPath { get; }

        public AbsolutePath EditorLogPath { get; }

        public AbsolutePath ConsoleLogPath { get; }
    }
}
