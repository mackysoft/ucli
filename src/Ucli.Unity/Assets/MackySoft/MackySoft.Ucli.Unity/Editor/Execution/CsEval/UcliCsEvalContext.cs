using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Project;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Execution context passed to <c>ucli.cs.eval</c> entry points. </summary>
    [UcliDescription("Execution context passed to ucli.cs.eval entry points.")]
    public sealed class UcliCsEvalContext
    {
        private const string ProjectSettingsRootPrefix = "ProjectSettings/";

        private const string SceneExtension = ".unity";

        private const string PrefabExtension = ".prefab";

        private readonly List<CsEvalLogEntry> logs = new List<CsEvalLogEntry>();

        private readonly List<CsEvalTouchedResourceDeclaration> touchedResources = new List<CsEvalTouchedResourceDeclaration>();

        private bool declaredNoTouchedResources;

        /// <summary> Records an informational eval log entry. </summary>
        /// <param name="message"> The log message text. </param>
        [UcliDescription("Records an informational eval log entry.")]
        public void Log ([UcliDescription("Log message text.")] string message)
        {
            AddLog(CsEvalLogLevelValues.Log, message);
        }

        /// <summary> Records a warning eval log entry. </summary>
        /// <param name="message"> The log message text. </param>
        [UcliDescription("Records a warning eval log entry.")]
        public void LogWarning ([UcliDescription("Log message text.")] string message)
        {
            AddLog(CsEvalLogLevelValues.Warning, message);
        }

        /// <summary> Records an error eval log entry. </summary>
        /// <param name="message"> The log message text. </param>
        [UcliDescription("Records an error eval log entry.")]
        public void LogError ([UcliDescription("Log message text.")] string message)
        {
            AddLog(CsEvalLogLevelValues.Error, message);
        }

        /// <summary> Declares that the eval call did not touch Unity resources. </summary>
        [UcliDescription("Declares that the eval call did not touch Unity resources.")]
        public void DeclareNoTouchedResources ()
        {
            if (touchedResources.Count != 0)
            {
                throw new InvalidOperationException("DeclareNoTouchedResources cannot be used after declaring touched resources.");
            }

            declaredNoTouchedResources = true;
        }

        /// <summary> Declares that the eval call touched a project asset. </summary>
        /// <param name="path"> The project-relative asset path. </param>
        [UcliDescription("Declares that the eval call touched a project asset.")]
        public void DeclareTouchedAsset ([UcliDescription("Project-relative asset path.")] string path)
        {
            var normalizedPath = NormalizeDeclaredPath(path, UnityAssetPathUtility.AssetsRootPrefix, requiredExtension: null);
            if (normalizedPath.EndsWith(SceneExtension, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.EndsWith(PrefabExtension, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Scene and prefab assets must be declared with their specific touched-resource APIs.", nameof(path));
            }

            AddTouchedResource(
                IpcExecuteTouchedResourceKindNames.Asset,
                normalizedPath);
        }

        /// <summary> Declares that the eval call touched a scene asset. </summary>
        /// <param name="path"> The project-relative scene asset path. </param>
        [UcliDescription("Declares that the eval call touched a scene asset.")]
        public void DeclareTouchedScene ([UcliDescription("Project-relative scene asset path.")] string path)
        {
            AddTouchedResource(
                IpcExecuteTouchedResourceKindNames.Scene,
                NormalizeDeclaredPath(path, UnityAssetPathUtility.AssetsRootPrefix, SceneExtension));
        }

        /// <summary> Declares that the eval call touched a prefab asset. </summary>
        /// <param name="path"> The project-relative prefab asset path. </param>
        [UcliDescription("Declares that the eval call touched a prefab asset.")]
        public void DeclareTouchedPrefab ([UcliDescription("Project-relative prefab asset path.")] string path)
        {
            AddTouchedResource(
                IpcExecuteTouchedResourceKindNames.Prefab,
                NormalizeDeclaredPath(path, UnityAssetPathUtility.AssetsRootPrefix, PrefabExtension));
        }

        /// <summary> Declares that the eval call touched a ProjectSettings asset. </summary>
        /// <param name="path"> The project-relative ProjectSettings path. </param>
        [UcliDescription("Declares that the eval call touched a ProjectSettings asset.")]
        public void DeclareTouchedProjectSettings ([UcliDescription("Project-relative ProjectSettings path.")] string path)
        {
            AddTouchedResource(
                IpcExecuteTouchedResourceKindNames.ProjectSettings,
                NormalizeDeclaredPath(path, ProjectSettingsRootPrefix, requiredExtension: null));
        }

        internal IReadOnlyList<CsEvalLogEntry> Logs => logs;

        internal bool DeclaredNoTouchedResources => declaredNoTouchedResources;

        internal IReadOnlyList<CsEvalTouchedResourceDeclaration> TouchedResources => touchedResources;

        private void AddLog (
            string level,
            string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Log message must not be empty.", nameof(message));
            }

            logs.Add(new CsEvalLogEntry(level, message));
        }

        private static string NormalizeDeclaredPath (
            string path,
            string requiredPrefix,
            string? requiredExtension)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Touched resource path must not be empty.", nameof(path));
            }

            if (!string.Equals(path, path.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException("Touched resource path must not contain leading or trailing whitespace.", nameof(path));
            }

            var normalizedPath = UnityAssetPathUtility.NormalizeAssetPath(path);
            if (normalizedPath.StartsWith("/", StringComparison.Ordinal)
                || normalizedPath.Contains(":", StringComparison.Ordinal))
            {
                throw new ArgumentException("Touched resource path must be project-relative.", nameof(path));
            }

            var segments = normalizedPath.Split('/');
            for (var i = 0; i < segments.Length; i++)
            {
                if (segments[i].Length == 0 || segments[i] == "." || segments[i] == "..")
                {
                    throw new ArgumentException("Touched resource path must not contain empty, current, or parent segments.", nameof(path));
                }
            }

            if (!normalizedPath.StartsWith(requiredPrefix, StringComparison.Ordinal)
                || normalizedPath.Length == requiredPrefix.Length)
            {
                throw new ArgumentException($"Touched resource path must be under '{requiredPrefix}'.", nameof(path));
            }

            if (requiredExtension != null
                && !normalizedPath.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Touched resource path must end with '{requiredExtension}'.", nameof(path));
            }

            return normalizedPath;
        }

        private void AddTouchedResource (
            string kind,
            string path)
        {
            if (declaredNoTouchedResources)
            {
                throw new InvalidOperationException("Touched resources cannot be declared after DeclareNoTouchedResources.");
            }

            touchedResources.Add(new CsEvalTouchedResourceDeclaration(kind, path));
        }
    }
}
