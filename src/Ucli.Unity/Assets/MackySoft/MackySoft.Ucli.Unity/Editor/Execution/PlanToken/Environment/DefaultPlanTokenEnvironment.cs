using MackySoft.Ucli.Infrastructure.Storage;
using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Contracts.Storage;
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
            var compileState = IpcCompileStateCodec.ToValue(EditorApplication.isCompiling);

            return new PlanTokenEnvironmentSnapshot(
                projectRoot: projectRoot,
                repositoryRoot: repositoryRoot,
                projectFingerprint: projectFingerprint,
                unityVersion: unityVersion,
                compileState: compileState,
                domainReloadGeneration: UnityEditorReadinessGate.CurrentDomainReloadGeneration);
        }
    }
}
