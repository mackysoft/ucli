using System;
using MackySoft.Ucli.Contracts.Ipc;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Validates and normalizes daemon <c>test.run</c> request payload values. </summary>
    internal sealed class UnityTestRunRequestContextFactory : IUnityTestRunRequestContextFactory
    {
        /// <summary> Creates one normalized request context from IPC payload values. </summary>
        /// <param name="request"> The decoded IPC request payload. </param>
        /// <returns> The normalized request context. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when payload violates contract. </exception>
        /// <exception cref="InvalidOperationException"> Thrown when required runtime context cannot be resolved. </exception>
        public UnityTestRunRequestContext Create (IpcTestRunRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.TestPlatform))
            {
                throw new ArgumentException("testPlatform must not be empty.", nameof(request));
            }

            if (request.TestCategories == null)
            {
                throw new ArgumentException("testCategories must not be null.", nameof(request));
            }

            if (request.AssemblyNames == null)
            {
                throw new ArgumentException("assemblyNames must not be null.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.ResultsXmlPath))
            {
                throw new ArgumentException("resultsXmlPath must not be empty.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.EditorLogPath))
            {
                throw new ArgumentException("editorLogPath must not be empty.", nameof(request));
            }

            var testMode = ParseTestMode(request.TestPlatform);
            var buildTarget = ParseBuildTargetOrNull(request.BuildTarget);
            if (testMode == TestMode.EditMode && buildTarget.HasValue)
            {
                throw new ArgumentException(
                    "buildTarget is not allowed when testPlatform=editmode.",
                    nameof(request));
            }

            var consoleLogPath = Application.consoleLogPath;
            if (string.IsNullOrWhiteSpace(consoleLogPath))
            {
                throw new InvalidOperationException("Application.consoleLogPath is empty.");
            }

            return new UnityTestRunRequestContext(
                TestMode: testMode,
                BuildTarget: buildTarget,
                TestFilter: request.TestFilter,
                TestCategories: request.TestCategories,
                AssemblyNames: request.AssemblyNames,
                ResultsXmlPath: request.ResultsXmlPath,
                EditorLogPath: request.EditorLogPath,
                ConsoleLogPath: consoleLogPath);
        }

        /// <summary> Parses test-platform string into Unity test mode. </summary>
        /// <param name="testPlatform"> The test-platform string. </param>
        /// <returns> The parsed Unity test mode. </returns>
        /// <exception cref="ArgumentException"> Thrown when test-platform value is invalid. </exception>
        private static TestMode ParseTestMode (string testPlatform)
        {
            if (!IpcTestRunPlatformCodec.TryParse(testPlatform, out var parsedTestPlatform))
            {
                throw new ArgumentException(
                    $"testPlatform must be {IpcTestRunPlatformCodec.EditMode} or {IpcTestRunPlatformCodec.PlayMode}. Actual: {testPlatform}");
            }

            if (parsedTestPlatform == IpcTestRunPlatform.EditMode)
            {
                return TestMode.EditMode;
            }

            return TestMode.PlayMode;
        }

        /// <summary> Parses optional build-target string into Unity build target enum. </summary>
        /// <param name="buildTarget"> The optional build-target value. </param>
        /// <returns> The parsed build target, or <see langword="null" /> when omitted. </returns>
        /// <exception cref="ArgumentException"> Thrown when build-target value is invalid. </exception>
        private static BuildTarget? ParseBuildTargetOrNull (string? buildTarget)
        {
            if (string.IsNullOrWhiteSpace(buildTarget))
            {
                return null;
            }

            if (!Enum.TryParse(buildTarget, ignoreCase: true, out BuildTarget parsedBuildTarget))
            {
                throw new ArgumentException($"buildTarget is invalid: {buildTarget}");
            }

            return parsedBuildTarget;
        }
    }
}
