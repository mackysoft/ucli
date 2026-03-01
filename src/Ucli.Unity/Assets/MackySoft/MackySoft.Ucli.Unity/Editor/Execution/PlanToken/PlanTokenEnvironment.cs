using System;
using System.IO;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Captures runtime values required for plan-token generation and validation. </summary>
    internal interface IPlanTokenEnvironment
    {
        /// <summary> Captures one runtime environment snapshot. </summary>
        /// <returns> The captured snapshot. </returns>
        PlanTokenEnvironmentSnapshot Capture ();

        /// <summary> Gets the current UTC time. </summary>
        DateTimeOffset UtcNow { get; }
    }

    /// <summary> Represents one captured runtime snapshot used by plan-token workflows. </summary>
    /// <param name="ProjectRoot"> The Unity project root path. </param>
    /// <param name="RepositoryRoot"> The repository root path. </param>
    /// <param name="ProjectFingerprint"> The deterministic project fingerprint. </param>
    /// <param name="UnityVersion"> The current Unity version. </param>
    /// <param name="CompileState"> The current compile state. </param>
    /// <param name="DomainReloadGeneration"> The current domain-reload generation marker. </param>
    internal sealed record PlanTokenEnvironmentSnapshot (
        string ProjectRoot,
        string RepositoryRoot,
        string ProjectFingerprint,
        string UnityVersion,
        string CompileState,
        string DomainReloadGeneration);

    /// <summary> Default runtime environment provider used by plan-token workflows. </summary>
    internal sealed class DefaultPlanTokenEnvironment : IPlanTokenEnvironment
    {
        /// <summary> Gets the current UTC clock value. </summary>
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        /// <summary> Captures one runtime environment snapshot from Unity editor state. </summary>
        /// <returns> The captured snapshot. </returns>
        public PlanTokenEnvironmentSnapshot Capture ()
        {
            var projectRoot = ResolveProjectRoot();
            var repositoryRoot = ResolveRepositoryRoot(projectRoot);
            var projectFingerprint = UnityProjectFingerprintCalculatorCompat.Create(repositoryRoot, projectRoot);

            var unityVersion = string.IsNullOrWhiteSpace(Application.unityVersion)
                ? "na"
                : Application.unityVersion;
            var compileState = EditorApplication.isCompiling ? "compiling" : "ready";

            return new PlanTokenEnvironmentSnapshot(
                ProjectRoot: projectRoot,
                RepositoryRoot: repositoryRoot,
                ProjectFingerprint: projectFingerprint,
                UnityVersion: unityVersion,
                CompileState: compileState,
                DomainReloadGeneration: "na");
        }

        /// <summary> Resolves the current Unity project root path. </summary>
        /// <returns> The project root path. </returns>
        private static string ResolveProjectRoot ()
        {
            var dataPath = Application.dataPath;
            if (string.IsNullOrWhiteSpace(dataPath))
            {
                return Directory.GetCurrentDirectory();
            }

            var projectRoot = Path.GetDirectoryName(Path.GetFullPath(dataPath));
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                return Directory.GetCurrentDirectory();
            }

            return projectRoot;
        }

        /// <summary> Resolves repository root by scanning parent directories for <c>.git</c>. </summary>
        /// <param name="projectRoot"> The Unity project root path. </param>
        /// <returns> The resolved repository root, or <paramref name="projectRoot" /> when no git marker is found. </returns>
        private static string ResolveRepositoryRoot (string projectRoot)
        {
            var currentDirectory = projectRoot;
            while (!string.IsNullOrWhiteSpace(currentDirectory))
            {
                var gitDirectoryPath = Path.Combine(currentDirectory, ".git");
                if (Directory.Exists(gitDirectoryPath) || File.Exists(gitDirectoryPath))
                {
                    return currentDirectory;
                }

                var parentDirectory = Directory.GetParent(currentDirectory);
                if (parentDirectory == null)
                {
                    break;
                }

                currentDirectory = parentDirectory.FullName;
            }

            return projectRoot;
        }
    }
}
