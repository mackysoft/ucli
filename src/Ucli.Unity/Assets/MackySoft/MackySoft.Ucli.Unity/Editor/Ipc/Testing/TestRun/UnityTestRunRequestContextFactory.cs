using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
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
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

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

            if (string.IsNullOrWhiteSpace(request.RunId))
            {
                throw new ArgumentException("runId must not be empty.", nameof(request));
            }

            var (testMode, targetPlatform) = ParseTestPlatform(request.TestPlatform);

            var consoleLogPath = Application.consoleLogPath;
            if (string.IsNullOrWhiteSpace(consoleLogPath))
            {
                throw new InvalidOperationException("Application.consoleLogPath is empty.");
            }

            return new UnityTestRunRequestContext(
                RunId: request.RunId!,
                TestPlatform: request.TestPlatform,
                TestMode: testMode,
                TargetPlatform: targetPlatform,
                TestFilter: request.TestFilter,
                TestCategories: request.TestCategories,
                AssemblyNames: request.AssemblyNames,
                ResultsXmlPath: request.ResultsXmlPath,
                EditorLogPath: request.EditorLogPath,
                ConsoleLogPath: consoleLogPath);
        }

        /// <summary> Parses test-platform string into Unity execution settings. </summary>
        /// <param name="testPlatform"> The test-platform string. </param>
        /// <returns> One tuple containing Unity test mode and optional player target platform. </returns>
        /// <exception cref="ArgumentException"> Thrown when test-platform value is invalid. </exception>
        private static (TestMode TestMode, BuildTarget? TargetPlatform) ParseTestPlatform (string testPlatform)
        {
            if (!TestRunPlatformCodec.TryParse(testPlatform, out var parsedTestPlatform))
            {
                throw new ArgumentException($"testPlatform must not be empty. Actual: {testPlatform}");
            }

            if (parsedTestPlatform.IsEditMode)
            {
                return (TestMode.EditMode, null);
            }

            if (parsedTestPlatform.IsPlayMode)
            {
                return (TestMode.PlayMode, null);
            }

            if (!Enum.TryParse(parsedTestPlatform.PlayerBuildTargetLiteral, ignoreCase: true, out BuildTarget parsedBuildTarget))
            {
                throw new ArgumentException($"testPlatform is invalid Unity BuildTarget: {parsedTestPlatform.PlayerBuildTargetLiteral}");
            }

            return (TestMode.PlayMode, parsedBuildTarget);
        }
    }
}
