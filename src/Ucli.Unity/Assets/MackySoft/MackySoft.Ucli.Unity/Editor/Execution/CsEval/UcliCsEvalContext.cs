using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Execution context passed to dedicated C# eval entry points. </summary>
    public sealed class UcliCsEvalContext
    {
        private const string ProjectSettingsRootPrefix = "ProjectSettings/";

        private const string TruncationSuffix = "...";

        private const int TruncationSuffixUtf8ByteCount = 3;

        private readonly List<CsEvalLogEntry> logs = new List<CsEvalLogEntry>();

        private readonly HashSet<string> scenes = new HashSet<string>(StringComparer.Ordinal);

        private readonly HashSet<string> prefabs = new HashSet<string>(StringComparer.Ordinal);

        private readonly HashSet<string> assets = new HashSet<string>(StringComparer.Ordinal);

        private readonly HashSet<string> projectSettings = new HashSet<string>(StringComparer.Ordinal);

        private bool declaredNoTouchedResources;

        private bool logsTruncated;

        /// <summary> Initializes the context passed to one evaluated entry point. </summary>
        internal UcliCsEvalContext (CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
        }

        /// <summary> Gets the cooperative cancellation signal for the current evaluation. </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary> Records a structured entry using the dedicated evaluation API. </summary>
        public void Log (UcliCsEvalLogLevel level, string message, object? data = null)
        {
            JsonElement? serializedData;
            try
            {
                serializedData = data is null
                    ? null
                    : JsonSerializer.SerializeToElement(data, data.GetType(), IpcJsonSerializerOptions.Default);
            }
            catch (Exception exception)
            {
                throw new ArgumentException("C# eval log data must be JSON-serializable.", nameof(data), exception);
            }

            AddLog(level switch
            {
                UcliCsEvalLogLevel.Debug => CsEvalLogLevel.Debug,
                UcliCsEvalLogLevel.Info => CsEvalLogLevel.Info,
                UcliCsEvalLogLevel.Warning => CsEvalLogLevel.Warning,
                UcliCsEvalLogLevel.Error => CsEvalLogLevel.Error,
                _ => throw new ArgumentOutOfRangeException(nameof(level)),
            }, message, serializedData);
        }

        /// <summary> Declares one touched scene. </summary>
        public void TouchScene (string path) => AddTouchedResource(UcliTouchedResourceKind.Scene, NormalizeDeclaredSceneAssetPath(path));

        /// <summary> Declares one touched prefab. </summary>
        public void TouchPrefab (string path) => AddTouchedResource(UcliTouchedResourceKind.Prefab, NormalizeDeclaredPrefabAssetPath(path));

        /// <summary> Declares one touched asset. </summary>
        public void TouchAsset (string path)
        {
            var normalizedPath = NormalizeDeclaredAssetPath(path);
            if (IsSceneOrPrefabAssetPath(normalizedPath)) throw new ArgumentException("Scene and prefab assets must be declared with their specific touched-resource APIs.", nameof(path));
            AddTouchedResource(UcliTouchedResourceKind.Asset, normalizedPath);
        }

        /// <summary> Declares one touched Project Settings file. </summary>
        public void TouchProjectSettings (string path) => AddTouchedResource(UcliTouchedResourceKind.ProjectSettings, NormalizeDeclaredPath(path, ProjectSettingsRootPrefix));

        /// <summary> Declares that the evaluation made no changes. </summary>
        public void DeclareNoChanges ()
        {
            if (HasTouchedResources)
            {
                throw new InvalidOperationException("DeclareNoChanges cannot be used after declaring touched resources.");
            }

            declaredNoTouchedResources = true;
        }

        internal IReadOnlyList<CsEvalLogEntry> Logs => logs;

        internal bool DeclaredNoTouchedResources => declaredNoTouchedResources;

        internal IReadOnlyCollection<string> Scenes => scenes;
        internal IReadOnlyCollection<string> Prefabs => prefabs;
        internal IReadOnlyCollection<string> Assets => assets;
        internal IReadOnlyCollection<string> ProjectSettings => projectSettings;
        internal bool HasTouchedResources => scenes.Count != 0 || prefabs.Count != 0 || assets.Count != 0 || projectSettings.Count != 0;
        internal bool HasImpactDeclaration => declaredNoTouchedResources || HasTouchedResources;

        private void AddLog (
            CsEvalLogLevel level,
            string message,
            JsonElement? data)
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

            logs.Add(new CsEvalLogEntry(logs.Count + 1, level, LimitUtf8(message, CsEvalSafetyLimits.MaxLogMessageBytes), data));
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
                throw new InvalidOperationException("Touched resources cannot be declared after DeclareNoChanges.");
            }

            if (scenes.Count + prefabs.Count + assets.Count + projectSettings.Count >= CsEvalSafetyLimits.MaxTouchedResources)
            {
                AddSystemWarning("C# eval touched resource declarations were truncated.");
                return;
            }

            switch (kind)
            {
                case UcliTouchedResourceKind.Scene: scenes.Add(path); break;
                case UcliTouchedResourceKind.Prefab: prefabs.Add(path); break;
                case UcliTouchedResourceKind.Asset: assets.Add(path); break;
                case UcliTouchedResourceKind.ProjectSettings: projectSettings.Add(path); break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private void AddSystemWarning (string message)
        {
            if (logs.Count >= CsEvalSafetyLimits.MaxLogEntries)
            {
                SetLogsTruncated();
                return;
            }

            logs.Add(new CsEvalLogEntry(logs.Count + 1, CsEvalLogLevel.Warning, message, null));
        }

        private void SetLogsTruncated ()
        {
            if (logsTruncated)
            {
                return;
            }

            logsTruncated = true;
            var warning = new CsEvalLogEntry(logs.Count == 0 ? 1 : logs[^1].Sequence, CsEvalLogLevel.Warning, "C# eval logs were truncated.", null);
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
