using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;

#nullable enable

namespace MackySoft.Ucli.Unity
{
    /// <summary> Represents the invocation context passed to a uCLI executeMethod build runner. </summary>
    public sealed class UcliBuildRunnerContext
    {
        /// <summary> Initializes a new instance of the <see cref="UcliBuildRunnerContext" /> class. </summary>
        internal UcliBuildRunnerContext (
            Guid runId,
            string projectPath,
            ProjectFingerprint projectFingerprint,
            string outputDir,
            string profilePath,
            Sha256Digest profileDigest,
            UcliResolvedBuildTarget target,
            IReadOnlyList<string> scenes,
            UcliBuildOptions options,
            IReadOnlyDictionary<string, string> arguments,
            IReadOnlyDictionary<string, string> environmentVariables,
            IReadOnlyDictionary<string, string> environmentSecrets)
        {
            if (runId == Guid.Empty)
            {
                throw new ArgumentException("runId must not be empty.", nameof(runId));
            }

            RunId = runId;
            ProjectPath = RequireValue(projectPath, nameof(projectPath));
            ProjectFingerprint = projectFingerprint ?? throw new ArgumentNullException(nameof(projectFingerprint));
            OutputDir = RequireValue(outputDir, nameof(outputDir));
            ProfilePath = RequireValue(profilePath, nameof(profilePath));
            ProfileDigest = profileDigest ?? throw new ArgumentNullException(nameof(profileDigest));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
            Environment = new UcliBuildRunnerEnvironment(environmentVariables, environmentSecrets);
        }

        /// <summary> Gets the current runner invocation context while a runner method is active. </summary>
        public static UcliBuildRunnerContext? Current { get; internal set; }

        /// <summary> Gets the uCLI build run identifier. </summary>
        public Guid RunId { get; }

        /// <summary> Gets the absolute Unity project root path. </summary>
        public string ProjectPath { get; }

        /// <summary> Gets the uCLI project fingerprint. </summary>
        public ProjectFingerprint ProjectFingerprint { get; }

        /// <summary> Gets the absolute runner working output directory. </summary>
        public string OutputDir { get; }

        /// <summary> Gets the resolved build profile path. </summary>
        public string ProfilePath { get; }

        /// <summary> Gets the canonical build profile digest. </summary>
        public Sha256Digest ProfileDigest { get; }

        /// <summary> Gets the resolved build target. </summary>
        public UcliResolvedBuildTarget Target { get; }

        /// <summary> Gets the resolved project-relative scene asset paths. </summary>
        public IReadOnlyList<string> Scenes { get; }

        /// <summary> Gets the resolved build options. </summary>
        public UcliBuildOptions Options { get; }

        /// <summary> Gets substitution-resolved non-secret runner arguments. </summary>
        public IReadOnlyDictionary<string, string> Arguments { get; }

        /// <summary> Gets process environment entries resolved by uCLI for this runner invocation. </summary>
        public UcliBuildRunnerEnvironment Environment { get; }

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
