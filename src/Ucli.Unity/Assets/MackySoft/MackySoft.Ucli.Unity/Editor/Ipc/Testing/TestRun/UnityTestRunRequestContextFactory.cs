using System;
using System.Collections.Generic;
using System.IO;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Infrastructure.Storage;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Validates and normalizes daemon <c>test.run</c> request payload values. </summary>
    internal sealed class UnityTestRunRequestContextFactory : IUnityTestRunRequestContextFactory
    {
        private readonly IpcProjectIdentity projectIdentity;

        /// <summary> Initializes a factory bound to the current Unity host project identity. </summary>
        /// <param name="projectIdentity"> The current Unity host project identity used to derive run-scoped artifact paths. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="projectIdentity" /> is <see langword="null" />. </exception>
        public UnityTestRunRequestContextFactory (IpcProjectIdentity projectIdentity)
        {
            this.projectIdentity = projectIdentity ?? throw new ArgumentNullException(nameof(projectIdentity));
        }

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

            var (testMode, targetPlatform) = ParseTestPlatform(request.TestPlatform);
            var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(projectIdentity.ProjectPath);
            var artifactsDirectoryPath = UcliStoragePathResolver.ResolveTestRunArtifactsDirectory(
                storageRoot,
                projectIdentity.ProjectFingerprint,
                request.RunId);

            var consoleLogPath = Application.consoleLogPath;
            if (string.IsNullOrWhiteSpace(consoleLogPath))
            {
                throw new InvalidOperationException("Application.consoleLogPath is empty.");
            }

            return new UnityTestRunRequestContext(
                runId: request.RunId,
                testPlatform: request.TestPlatform,
                testMode: testMode,
                targetPlatform: targetPlatform,
                testFilter: request.TestFilter,
                testCategories: CopyToArray(request.TestCategories),
                assemblyNames: CopyToArray(request.AssemblyNames),
                resultsXmlPath: Path.Combine(artifactsDirectoryPath, UcliStoragePathNames.TestResultsXmlFileName),
                editorLogPath: Path.Combine(artifactsDirectoryPath, UcliStoragePathNames.TestEditorLogFileName),
                consoleLogPath: consoleLogPath);
        }

        private static string[] CopyToArray (IReadOnlyList<string> values)
        {
            var result = new string[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                result[index] = values[index];
            }

            return result;
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
