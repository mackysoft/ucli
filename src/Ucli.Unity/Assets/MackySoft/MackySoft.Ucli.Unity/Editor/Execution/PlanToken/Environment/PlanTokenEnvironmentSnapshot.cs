#nullable enable

using System;
using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Represents one validated runtime snapshot used by plan-token workflows. </summary>
    internal sealed record PlanTokenEnvironmentSnapshot
    {
        public PlanTokenEnvironmentSnapshot (
            string projectRoot,
            string repositoryRoot,
            ProjectFingerprint projectFingerprint,
            string unityVersion,
            string compileState,
            string domainReloadGeneration)
        {
            ProjectRoot = RequireValue(projectRoot, nameof(projectRoot));
            RepositoryRoot = RequireValue(repositoryRoot, nameof(repositoryRoot));
            ProjectFingerprint = projectFingerprint ?? throw new ArgumentNullException(nameof(projectFingerprint));
            UnityVersion = RequireValue(unityVersion, nameof(unityVersion));
            CompileState = RequireValue(compileState, nameof(compileState));
            DomainReloadGeneration = RequireValue(domainReloadGeneration, nameof(domainReloadGeneration));
        }

        public string ProjectRoot { get; }

        public string RepositoryRoot { get; }

        public ProjectFingerprint ProjectFingerprint { get; }

        public string UnityVersion { get; }

        public string CompileState { get; }

        public string DomainReloadGeneration { get; }

        private static string RequireValue (
            string value,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{parameterName} must not be empty.", parameterName);
            }

            return value;
        }
    }
}
