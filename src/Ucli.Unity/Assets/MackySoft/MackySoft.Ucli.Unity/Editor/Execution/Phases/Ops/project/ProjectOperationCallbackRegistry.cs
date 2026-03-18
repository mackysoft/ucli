using System;
using System.Collections.Generic;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Collects asset-path notifications emitted by Unity editor callbacks during project-domain operations. </summary>
    internal static class ProjectOperationCallbackRegistry
    {
        private static readonly CallbackScopeState RefreshState = new();

        private static readonly CallbackScopeState SaveState = new();

        /// <summary> Begins one refresh callback-capture scope. </summary>
        /// <returns> The scope identifier used to finish capture. </returns>
        public static int BeginRefreshCapture ()
        {
            return RefreshState.BeginScope();
        }

        /// <summary> Ends one refresh callback-capture scope. </summary>
        /// <param name="scopeId"> The capture scope identifier. </param>
        /// <returns> The stable, deduplicated callback path list. </returns>
        public static IReadOnlyList<string> EndRefreshCapture (int scopeId)
        {
            return RefreshState.EndScope(scopeId);
        }

        /// <summary> Records refresh callback paths into all active refresh scopes. </summary>
        /// <param name="importedAssets"> The imported asset paths. </param>
        /// <param name="deletedAssets"> The deleted asset paths. </param>
        /// <param name="movedAssets"> The moved-to asset paths. </param>
        /// <param name="movedFromAssetPaths"> The moved-from asset paths. </param>
        public static void RecordRefreshPaths (
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            RefreshState.Record(importedAssets);
            RefreshState.Record(deletedAssets);
            RefreshState.Record(movedAssets);
            RefreshState.Record(movedFromAssetPaths);
        }

        /// <summary> Begins one save callback-capture scope. </summary>
        /// <returns> The scope identifier used to finish capture. </returns>
        public static int BeginSaveCapture ()
        {
            return SaveState.BeginScope();
        }

        /// <summary> Ends one save callback-capture scope. </summary>
        /// <param name="scopeId"> The capture scope identifier. </param>
        /// <returns> The stable, deduplicated callback path list. </returns>
        public static IReadOnlyList<string> EndSaveCapture (int scopeId)
        {
            return SaveState.EndScope(scopeId);
        }

        /// <summary> Records save callback paths into all active save scopes. </summary>
        /// <param name="paths"> The save callback paths. </param>
        /// <returns> The original path array required by Unity callback contract. </returns>
        public static string[] RecordSavePaths (string[] paths)
        {
            SaveState.Record(paths);
            return paths;
        }

        private sealed class CallbackScopeState
        {
            private readonly object gate = new();

            private readonly Dictionary<int, HashSet<string>> activeScopes = new();

            private int nextScopeId;

            /// <summary> Begins one callback-capture scope. </summary>
            /// <returns> The scope identifier. </returns>
            public int BeginScope ()
            {
                lock (gate)
                {
                    checked
                    {
                        nextScopeId++;
                    }

                    activeScopes.Add(nextScopeId, new HashSet<string>(StringComparer.Ordinal));
                    return nextScopeId;
                }
            }

            /// <summary> Ends one callback-capture scope and returns collected paths. </summary>
            /// <param name="scopeId"> The scope identifier. </param>
            /// <returns> The stable collected path list. </returns>
            public IReadOnlyList<string> EndScope (int scopeId)
            {
                lock (gate)
                {
                    if (!activeScopes.Remove(scopeId, out var collectedPaths))
                    {
                        return Array.Empty<string>();
                    }

                    var result = new string[collectedPaths.Count];
                    collectedPaths.CopyTo(result);
                    Array.Sort(result, StringComparer.Ordinal);
                    return result;
                }
            }

            /// <summary> Records callback paths into all currently active scopes. </summary>
            /// <param name="paths"> The callback path batch. </param>
            public void Record (string[]? paths)
            {
                if (paths == null || paths.Length == 0)
                {
                    return;
                }

                lock (gate)
                {
                    if (activeScopes.Count == 0)
                    {
                        return;
                    }

                    foreach (var path in paths)
                    {
                        if (string.IsNullOrWhiteSpace(path))
                        {
                            continue;
                        }

                        foreach (var scope in activeScopes.Values)
                        {
                            scope.Add(path);
                        }
                    }
                }
            }
        }
    }
}