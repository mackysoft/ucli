using System;
using System.Collections.Generic;
using System.Text;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Operations;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Execution context passed to <c>ucli.cs.eval</c> entry points. </summary>
    [UcliDescription("Execution context passed to ucli.cs.eval entry points.")]
    public sealed class UcliCsEvalContext
    {
        private const string ProjectSettingsRootPrefix = "ProjectSettings/";

        private const string TruncationSuffix = "...";

        private const int TruncationSuffixUtf8ByteCount = 3;

        private readonly List<CsEvalLogEntry> logs = new List<CsEvalLogEntry>();

        private readonly List<CsEvalTouchedResourceDeclaration> touchedResources = new List<CsEvalTouchedResourceDeclaration>();

        private bool declaredNoTouchedResources;

        private bool logsTruncated;

        private bool touchedResourcesTruncated;

        /// <summary> Records an informational eval log entry. </summary>
        /// <param name="message"> The log message text. </param>
        [UcliDescription("Records an informational eval log entry.")]
        public void Log ([UcliDescription("Log message text.")] string message)
        {
            AddLog(CsEvalLogLevel.Log, message);
        }

        /// <summary> Records a warning eval log entry. </summary>
        /// <param name="message"> The log message text. </param>
        [UcliDescription("Records a warning eval log entry.")]
        public void LogWarning ([UcliDescription("Log message text.")] string message)
        {
            AddLog(CsEvalLogLevel.Warning, message);
        }

        /// <summary> Records an error eval log entry. </summary>
        /// <param name="message"> The log message text. </param>
        [UcliDescription("Records an error eval log entry.")]
        public void LogError ([UcliDescription("Log message text.")] string message)
        {
            AddLog(CsEvalLogLevel.Error, message);
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
            var normalizedPath = NormalizeDeclaredAssetPath(path);
            if (IsSceneOrPrefabAssetPath(normalizedPath))
            {
                throw new ArgumentException("Scene and prefab assets must be declared with their specific touched-resource APIs.", nameof(path));
            }

            AddTouchedResource(
                UcliTouchedResourceKind.Asset,
                normalizedPath);
        }

        /// <summary> Declares that the eval call touched a scene asset. </summary>
        /// <param name="path"> The project-relative scene asset path. </param>
        [UcliDescription("Declares that the eval call touched a scene asset.")]
        public void DeclareTouchedScene ([UcliDescription("Project-relative scene asset path.")] string path)
        {
            AddTouchedResource(
                UcliTouchedResourceKind.Scene,
                NormalizeDeclaredSceneAssetPath(path));
        }

        /// <summary> Declares that the eval call touched a prefab asset. </summary>
        /// <param name="path"> The project-relative prefab asset path. </param>
        [UcliDescription("Declares that the eval call touched a prefab asset.")]
        public void DeclareTouchedPrefab ([UcliDescription("Project-relative prefab asset path.")] string path)
        {
            AddTouchedResource(
                UcliTouchedResourceKind.Prefab,
                NormalizeDeclaredPrefabAssetPath(path));
        }

        /// <summary> Declares that the eval call touched a ProjectSettings asset. </summary>
        /// <param name="path"> The project-relative ProjectSettings path. </param>
        [UcliDescription("Declares that the eval call touched a ProjectSettings asset.")]
        public void DeclareTouchedProjectSettings ([UcliDescription("Project-relative ProjectSettings path.")] string path)
        {
            AddTouchedResource(
                UcliTouchedResourceKind.ProjectSettings,
                NormalizeDeclaredPath(path, ProjectSettingsRootPrefix));
        }

        internal IReadOnlyList<CsEvalLogEntry> Logs => logs;

        internal bool DeclaredNoTouchedResources => declaredNoTouchedResources;

        internal IReadOnlyList<CsEvalTouchedResourceDeclaration> TouchedResources => touchedResources;

        internal bool TouchedResourcesTruncated => touchedResourcesTruncated;

        private void AddLog (
            CsEvalLogLevel level,
            string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Log message must not be empty.", nameof(message));
            }

            if (logs.Count >= CsEvalSafetyLimits.MaxLogEntries)
            {
                SetLogsTruncated();
                return;
            }

            logs.Add(new CsEvalLogEntry(level, LimitUtf8(message, CsEvalSafetyLimits.MaxLogMessageBytes)));
        }

        private static string NormalizeDeclaredPath (
            string path,
            string requiredPrefix)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Touched resource path must not be empty.", nameof(path));
            }

            if (!RelativePathContract.TryNormalize(path, out var normalizedPath))
            {
                throw new ArgumentException("Touched resource path must be project-relative and must not contain leading or trailing whitespace, empty segments, current segments, or parent segments.", nameof(path));
            }

            if (!normalizedPath.StartsWith(requiredPrefix, StringComparison.Ordinal)
                || normalizedPath.Length == requiredPrefix.Length)
            {
                throw new ArgumentException($"Touched resource path must be under '{requiredPrefix}'.", nameof(path));
            }

            return normalizedPath;
        }

        private static string NormalizeDeclaredAssetPath (
            string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Touched resource path must not be empty.", nameof(path));
            }

            if (!UnityAssetPathContract.TryNormalizeAssetsDescendantPath(path, out var normalizedPath))
            {
                throw new ArgumentException("Touched resource path must be under 'Assets/' and must not contain leading or trailing whitespace, empty segments, current segments, or parent segments.", nameof(path));
            }

            return normalizedPath;
        }

        private static string NormalizeDeclaredSceneAssetPath (string path)
        {
            if (!UnityAssetPathContract.TryNormalizeSceneAssetPath(path, out var normalizedPath))
            {
                throw new ArgumentException($"Touched resource path must be a scene asset path ending with '{UnityAssetPathContract.SceneAssetExtension}'.", nameof(path));
            }

            return normalizedPath;
        }

        private static string NormalizeDeclaredPrefabAssetPath (string path)
        {
            if (!UnityAssetPathContract.TryNormalizePrefabAssetPath(path, out var normalizedPath))
            {
                throw new ArgumentException($"Touched resource path must be a prefab asset path ending with '{UnityAssetPathContract.PrefabAssetExtension}'.", nameof(path));
            }

            return normalizedPath;
        }

        private static bool IsSceneOrPrefabAssetPath (string normalizedPath)
        {
            return normalizedPath.EndsWith(UnityAssetPathContract.SceneAssetExtension, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.EndsWith(UnityAssetPathContract.PrefabAssetExtension, StringComparison.OrdinalIgnoreCase);
        }

        private void AddTouchedResource (
            UcliTouchedResourceKind kind,
            string path)
        {
            if (declaredNoTouchedResources)
            {
                throw new InvalidOperationException("Touched resources cannot be declared after DeclareNoTouchedResources.");
            }

            if (touchedResources.Count >= CsEvalSafetyLimits.MaxTouchedResources)
            {
                touchedResourcesTruncated = true;
                AddSystemWarning("C# eval touched resource declarations were truncated.");
                return;
            }

            touchedResources.Add(new CsEvalTouchedResourceDeclaration(kind, path));
        }

        private void AddSystemWarning (string message)
        {
            if (logs.Count >= CsEvalSafetyLimits.MaxLogEntries)
            {
                SetLogsTruncated();
                return;
            }

            logs.Add(new CsEvalLogEntry(CsEvalLogLevel.Warning, message));
        }

        private void SetLogsTruncated ()
        {
            if (logsTruncated)
            {
                return;
            }

            logsTruncated = true;
            var warning = new CsEvalLogEntry(CsEvalLogLevel.Warning, "C# eval logs were truncated.");
            if (logs.Count == 0)
            {
                logs.Add(warning);
                return;
            }

            logs[logs.Count - 1] = warning;
        }

        private static string LimitUtf8 (
            string value,
            int maxBytes)
        {
            if (Encoding.UTF8.GetByteCount(value) <= maxBytes)
            {
                return value;
            }

            var contentByteLimit = maxBytes - TruncationSuffixUtf8ByteCount;
            var prefixLength = 0;
            var bytes = 0;
            while (prefixLength < value.Length)
            {
                var character = value[prefixLength];
                var characterLength = char.IsHighSurrogate(character)
                    && prefixLength + 1 < value.Length
                    && char.IsLowSurrogate(value[prefixLength + 1])
                        ? 2
                        : 1;
                var characterBytes = characterLength == 2
                    ? 4
                    : GetUtf8ByteCount(character);
                if (bytes + characterBytes > contentByteLimit)
                {
                    break;
                }

                prefixLength += characterLength;
                bytes += characterBytes;
            }

            return string.Create(
                prefixLength + TruncationSuffix.Length,
                (Value: value, PrefixLength: prefixLength),
                static (destination, state) =>
                {
                    state.Value.AsSpan(0, state.PrefixLength).CopyTo(destination);
                    TruncationSuffix.AsSpan().CopyTo(destination[state.PrefixLength..]);
                });
        }

        private static int GetUtf8ByteCount (char character)
        {
            if (character <= '\u007F')
            {
                return 1;
            }

            if (character <= '\u07FF')
            {
                return 2;
            }

            return 3;
        }
    }
}
