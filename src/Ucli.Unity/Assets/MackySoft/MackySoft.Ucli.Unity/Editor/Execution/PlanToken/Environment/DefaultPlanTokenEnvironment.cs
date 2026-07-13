using System;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Project;
using MackySoft.Ucli.Unity.Runtime;
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
            var projectRoot = UnityProjectPathResolver.ResolveProjectRootPath();
            var repositoryRoot = UcliStoragePathResolver.ResolveStorageRoot(projectRoot);
            var projectFingerprint = UnityProjectFingerprintCalculator.Create(repositoryRoot, projectRoot);

            var unityVersion = string.IsNullOrWhiteSpace(Application.unityVersion)
                ? "na"
                : Application.unityVersion;
            var compileState = UnityEditorCompileStateResolver.Resolve(
                EditorApplication.isCompiling,
                hasCompileFailure: false);

            return new PlanTokenEnvironmentSnapshot(
                ProjectRoot: projectRoot,
                RepositoryRoot: repositoryRoot,
                ProjectFingerprint: projectFingerprint,
                UnityVersion: unityVersion,
                CompileState: compileState,
                DomainReloadGeneration: UnityEditorReadinessGate.CurrentDomainReloadGeneration);
        }
    }
}
