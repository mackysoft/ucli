using System;
using System.IO;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Project;
using MackySoft.Ucli.Contracts.Storage;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
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
            var repositoryRoot = UcliStoragePathResolver.ResolveStorageRoot(projectRoot);
            var projectFingerprint = UnityProjectFingerprintCalculator.Create(repositoryRoot, projectRoot);

            var unityVersion = string.IsNullOrWhiteSpace(Application.unityVersion)
                ? "na"
                : Application.unityVersion;
            var compileState = IpcCompileStateCodec.ToValue(EditorApplication.isCompiling);

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

    }
}
