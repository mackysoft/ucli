using System;
using System.Collections.Generic;
using UnityEditor.Compilation;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Resolves assembly-name sets grouped by Unity editor/runtime classification. </summary>
    internal static class IndexAssemblyNameSetResolver
    {
        /// <summary> Resolves runtime assembly names. </summary>
        /// <returns> The runtime assembly names. </returns>
        public static HashSet<string> ResolveRuntimeAssemblyNames ()
        {
            return ResolveAssemblyNames(includeEditorAssemblies: false);
        }

        /// <summary> Resolves editor assembly names. </summary>
        /// <returns> The editor assembly names. </returns>
        public static HashSet<string> ResolveEditorAssemblyNames ()
        {
            return ResolveAssemblyNames(includeEditorAssemblies: true);
        }

        private static HashSet<string> ResolveAssemblyNames (bool includeEditorAssemblies)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assembly in CompilationPipeline.GetAssemblies())
            {
                var isEditorAssembly = (assembly.flags & AssemblyFlags.EditorAssembly) != 0;
                if (isEditorAssembly != includeEditorAssemblies)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(assembly.name))
                {
                    names.Add(assembly.name);
                }
            }

            return names;
        }
    }
}