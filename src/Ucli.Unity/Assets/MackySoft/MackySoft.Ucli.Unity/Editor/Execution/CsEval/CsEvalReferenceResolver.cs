using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Resolves Roslyn metadata references from assemblies loaded in the Unity editor process. </summary>
    internal sealed class CsEvalReferenceResolver
    {
        public CsEvalReferenceSet Resolve ()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var assembliesByPath = new SortedDictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < assemblies.Length; i++)
            {
                if (TryGetAssemblyLocation(assemblies[i], out var path))
                {
                    assembliesByPath[path] = assemblies[i];
                }
            }

            var references = assembliesByPath.Keys
                .Select(static path => MetadataReference.CreateFromFile(path))
                .Cast<MetadataReference>()
                .ToArray();
            var identity = string.Join(
                "\n",
                assembliesByPath.Select(static pair => CreateReferenceIdentity(pair.Value, pair.Key)).OrderBy(static value => value, StringComparer.Ordinal));
            return new CsEvalReferenceSet(references, identity);
        }

        private static bool TryGetAssemblyLocation (
            Assembly assembly,
            out string path)
        {
            path = string.Empty;
            if (assembly.IsDynamic)
            {
                return false;
            }

            try
            {
                path = assembly.Location;
            }
            catch (NotSupportedException)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }

        private static string CreateReferenceIdentity (
            Assembly assembly,
            string path)
        {
            var fileInfo = new FileInfo(path);
            try
            {
                var assemblyName = AssemblyName.GetAssemblyName(path);
                var moduleVersionId = assembly.ManifestModule.ModuleVersionId.ToString("D");
                return $"{assemblyName.Name}/{assemblyName.Version}/{moduleVersionId}/{fileInfo.Length}/{fileInfo.LastWriteTimeUtc.Ticks}";
            }
            catch (Exception exception) when (exception is BadImageFormatException or FileLoadException or FileNotFoundException)
            {
                return $"{Path.GetFileName(path)}/{fileInfo.Length}/{fileInfo.LastWriteTimeUtc.Ticks}";
            }
        }
    }
}
