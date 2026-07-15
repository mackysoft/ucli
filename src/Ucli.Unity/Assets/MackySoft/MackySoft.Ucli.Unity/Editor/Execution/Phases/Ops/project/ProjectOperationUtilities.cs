using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Infrastructure.Paths;
using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Provides shared helpers for project-domain phase operations. </summary>
    internal static class ProjectOperationUtilities
    {
        private const string ProjectSettingsRootPrefix = "ProjectSettings/";

        private const string MetaExtension = ".meta";

        /// <summary> Validates that one operation argument payload is a strict empty object. </summary>
        /// <param name="args"> The operation argument element. </param>
        /// <param name="errorMessage"> The validation error message when failed. </param>
        /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
        public static bool TryValidateEmptyArguments (
            JsonElement args,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            if (args.ValueKind != JsonValueKind.Object)
            {
                errorMessage = "Operation 'args' must be an object.";
                return false;
            }

            foreach (var property in args.EnumerateObject())
            {
                errorMessage = $"Operation 'args' contains an unknown property: {property.Name}.";
                return false;
            }

            return true;
        }

        /// <summary> Captures the current file-state snapshot under <c>ProjectSettings/</c>. </summary>
        /// <param name="projectRoot"> The absolute Unity project root path. </param>
        /// <returns> The snapshot keyed by project-relative path. </returns>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="projectRoot" /> is null, empty, or whitespace. </exception>
        public static IReadOnlyDictionary<string, ProjectOperationFileSnapshot> CaptureProjectSettingsSnapshot (string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("Project root must not be empty.", nameof(projectRoot));
            }

            var projectSettingsDirectoryPath = Path.Combine(projectRoot, "ProjectSettings");
            var snapshot = new Dictionary<string, ProjectOperationFileSnapshot>(StringComparer.Ordinal);
            if (!Directory.Exists(projectSettingsDirectoryPath))
            {
                return snapshot;
            }

            foreach (var filePath in Directory.EnumerateFiles(projectSettingsDirectoryPath, "*", SearchOption.AllDirectories))
            {
                var fileInfo = new FileInfo(filePath);
                var relativePathResult = RepositoryPathNormalizer.TryNormalize(projectRoot, filePath);
                if (!relativePathResult.IsSuccess)
                {
                    throw new InvalidOperationException(relativePathResult.DiagnosticMessage);
                }

                snapshot[relativePathResult.RepositoryRelativeSlashPath!] = new ProjectOperationFileSnapshot(
                    fileInfo.Length,
                    fileInfo.LastWriteTimeUtc.Ticks);
            }

            return snapshot;
        }

        /// <summary> Computes the stable changed-path list between two project-settings snapshots. </summary>
        /// <param name="before"> The pre-operation snapshot. </param>
        /// <param name="after"> The post-operation snapshot. </param>
        /// <returns> The stable changed-path list. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="before" /> or <paramref name="after" /> is <see langword="null" />. </exception>
        public static IReadOnlyList<string> GetChangedProjectSettingsPaths (
            IReadOnlyDictionary<string, ProjectOperationFileSnapshot> before,
            IReadOnlyDictionary<string, ProjectOperationFileSnapshot> after)
        {
            if (before == null)
            {
                throw new ArgumentNullException(nameof(before));
            }

            if (after == null)
            {
                throw new ArgumentNullException(nameof(after));
            }

            var changedPaths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var beforeEntry in before)
            {
                if (!after.TryGetValue(beforeEntry.Key, out var afterSnapshot)
                    || !afterSnapshot.Equals(beforeEntry.Value))
                {
                    changedPaths.Add(beforeEntry.Key);
                }
            }

            foreach (var afterEntry in after)
            {
                if (!before.TryGetValue(afterEntry.Key, out var beforeSnapshot)
                    || !beforeSnapshot.Equals(afterEntry.Value))
                {
                    changedPaths.Add(afterEntry.Key);
                }
            }

            var result = new string[changedPaths.Count];
            changedPaths.CopyTo(result);
            return result;
        }

        /// <summary> Creates the stable touched-resource list from callback paths and project-settings diffs. </summary>
        /// <param name="callbackPaths"> The paths collected from Unity editor callbacks. </param>
        /// <param name="projectSettingsPaths"> The changed project-settings paths. </param>
        /// <returns> The stable touched-resource list. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when any argument is <see langword="null" />. </exception>
        public static IReadOnlyList<OperationTouch> CreateTouchedResources (
            IReadOnlyList<string> callbackPaths,
            IReadOnlyList<string> projectSettingsPaths)
        {
            if (callbackPaths == null)
            {
                throw new ArgumentNullException(nameof(callbackPaths));
            }

            if (projectSettingsPaths == null)
            {
                throw new ArgumentNullException(nameof(projectSettingsPaths));
            }

            // NOTE:
            // Unity exposes asset import/save paths through editor callbacks, but project-settings writes are not
            // surfaced consistently through the same callbacks. We merge callback output with an explicit
            // ProjectSettings snapshot diff so touched results stay precise without broad filesystem scans under Assets/.
            var touchedByPath = new SortedDictionary<string, OperationTouch>(StringComparer.Ordinal);
            AddTouchedCandidates(callbackPaths, touchedByPath);
            AddTouchedCandidates(projectSettingsPaths, touchedByPath);

            var touched = new OperationTouch[touchedByPath.Count];
            var index = 0;
            foreach (var entry in touchedByPath.Values)
            {
                touched[index] = entry;
                index++;
            }

            return touched;
        }

        /// <summary> Synchronizes scene/prefab dirty-state transitions into touched resources and request-attributed change markers. </summary>
        /// <param name="beforeState"> The dirty-state snapshot captured before the project-domain operation. </param>
        /// <param name="afterState"> The dirty-state snapshot captured after the project-domain operation. </param>
        /// <param name="touchKind"> The persistence kind represented by the dirty-state map. </param>
        /// <param name="touched"> The touched-resource sink that receives state-transition entries. </param>
        /// <param name="executionContext"> The per-request execution context that tracks request-attributed changes. </param>
        /// <exception cref="ArgumentNullException"> Thrown when any reference argument is <see langword="null" />. </exception>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="touchKind" /> is not Scene or Prefab. </exception>
        public static void SyncDirtyStateChanges (
            IReadOnlyDictionary<string, bool> beforeState,
            IReadOnlyDictionary<string, bool> afterState,
            UcliTouchedResourceKind touchKind,
            ICollection<OperationTouch> touched,
            OperationExecutionContext executionContext)
        {
            if (beforeState == null)
            {
                throw new ArgumentNullException(nameof(beforeState));
            }

            if (afterState == null)
            {
                throw new ArgumentNullException(nameof(afterState));
            }

            if (touched == null)
            {
                throw new ArgumentNullException(nameof(touched));
            }

            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            if (touchKind != UcliTouchedResourceKind.Scene
                && touchKind != UcliTouchedResourceKind.Prefab)
            {
                throw new ArgumentOutOfRangeException(nameof(touchKind), touchKind, "Dirty-state syncing supports only scene and prefab resources.");
            }

            foreach (var pair in afterState)
            {
                if (beforeState.TryGetValue(pair.Key, out var beforeDirty)
                    && beforeDirty == pair.Value)
                {
                    continue;
                }

                if (touchKind == UcliTouchedResourceKind.Scene)
                {
                    touched.Add(OperationResourceUtilities.CreateTouch(new OperationResource(UcliTouchedResourceKind.Scene, pair.Key)));
                }
                else
                {
                    touched.Add(OperationResourceUtilities.CreateTouch(new OperationResource(UcliTouchedResourceKind.Prefab, pair.Key)));
                }

                var resource = new OperationResource(touchKind, pair.Key);
                if (pair.Value)
                {
                    executionContext.MarkRequestAttributedChange(resource);
                }
                else
                {
                    executionContext.UnmarkRequestAttributedChange(resource);
                }
            }
        }

        /// <summary> Adds touched candidates into the deduplication map. </summary>
        /// <param name="candidatePaths"> The candidate path collection. </param>
        /// <param name="touchedByPath"> The deduplication map keyed by touched path. </param>
        private static void AddTouchedCandidates (
            IReadOnlyList<string> candidatePaths,
            SortedDictionary<string, OperationTouch> touchedByPath)
        {
            for (var index = 0; index < candidatePaths.Count; index++)
            {
                if (!TryCreateTouchedResource(candidatePaths[index], out var touchedResource))
                {
                    continue;
                }

                if (touchedByPath.TryGetValue(touchedResource.Path, out var existing)
                    && existing.AssetGuid.HasValue)
                {
                    continue;
                }

                touchedByPath[touchedResource.Path] = touchedResource;
            }
        }

        /// <summary> Tries to create one touched-resource entry from one project-relative path candidate. </summary>
        /// <param name="candidatePath"> The candidate project-relative path. </param>
        /// <param name="touchedResource"> The touched-resource entry when successful. </param>
        /// <returns> <see langword="true" /> when the path maps to a supported persistence unit; otherwise <see langword="false" />. </returns>
        private static bool TryCreateTouchedResource (
            string? candidatePath,
            out OperationTouch touchedResource)
        {
            touchedResource = default!;
            if (string.IsNullOrWhiteSpace(candidatePath))
            {
                return false;
            }

            if (!RelativePathContract.TryNormalize(candidatePath, out var normalizedPath))
            {
                return false;
            }

            if (normalizedPath.EndsWith(MetaExtension, StringComparison.Ordinal))
            {
                normalizedPath = normalizedPath.Substring(0, normalizedPath.Length - MetaExtension.Length);
            }

            if (string.IsNullOrEmpty(normalizedPath))
            {
                return false;
            }

            if (normalizedPath.StartsWith(ProjectSettingsRootPrefix, StringComparison.Ordinal))
            {
                touchedResource = OperationResourceUtilities.CreateTouch(
                    new OperationResource(UcliTouchedResourceKind.ProjectSettings, normalizedPath));
                return true;
            }

            if (!UnityAssetPathContract.IsNormalizedAssetsDescendantPath(normalizedPath))
            {
                return false;
            }

            touchedResource = OperationResourceUtilities.CreateTouch(
                new OperationResource(ResolveAssetTouchKind(normalizedPath), normalizedPath));
            return true;
        }

        /// <summary> Resolves one asset touch kind from its project-relative path. </summary>
        /// <param name="assetPath"> The asset path. </param>
        /// <returns> The touched resource kind. </returns>
        private static UcliTouchedResourceKind ResolveAssetTouchKind (string assetPath)
        {
            if (UnityAssetPathContract.IsNormalizedSceneAssetPath(assetPath))
            {
                return UcliTouchedResourceKind.Scene;
            }

            if (UnityAssetPathContract.IsNormalizedPrefabAssetPath(assetPath))
            {
                return UcliTouchedResourceKind.Prefab;
            }

            return UcliTouchedResourceKind.Asset;
        }


    }
}
