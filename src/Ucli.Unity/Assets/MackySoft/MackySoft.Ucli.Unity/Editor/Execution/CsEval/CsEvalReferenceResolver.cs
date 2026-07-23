using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using MackySoft.FileSystem;
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
            var assembliesByPath = new Dictionary<AbsolutePath, Assembly>();
            for (var i = 0; i < assemblies.Length; i++)
            {
                if (TryGetAssemblyLocation(assemblies[i], out var path))
                {
                    assembliesByPath[path] = assemblies[i];
                }
            }

            var orderedAssembliesByPath = assembliesByPath
                .OrderBy(static pair => pair.Key.Value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var references = orderedAssembliesByPath
                .Select(static pair => MetadataReference.CreateFromFile(pair.Key.Value))
                .Cast<MetadataReference>()
                .ToArray();
            var identity = string.Join(
                "\n",
                orderedAssembliesByPath
                    .Select(static pair => CreateReferenceIdentity(pair.Value, pair.Key))
                    .OrderBy(static value => value, StringComparer.Ordinal));
            return new CsEvalReferenceSet(references, identity);
        }

        private static bool TryGetAssemblyLocation (
            Assembly assembly,
            [NotNullWhen(true)] out AbsolutePath? path)
        {
            path = null;
            if (assembly.IsDynamic)
            {
                return false;
            }

            string location;
            try
            {
                location = assembly.Location;
            }
            catch (NotSupportedException)
            {
                return false;
            }

            if (!AbsolutePath.TryParse(location, out var guardedLocation, out _)
                || !File.Exists(guardedLocation.Value))
            {
                return false;
            }

            path = guardedLocation;
            return true;
        }

        private static string CreateReferenceIdentity (
            Assembly assembly,
            AbsolutePath path)
        {
            var fileInfo = new FileInfo(path.Value);
            try
            {
                var assemblyName = AssemblyName.GetAssemblyName(path.Value);
                var moduleVersionId = assembly.ManifestModule.ModuleVersionId.ToString("D");
                return $"{assemblyName.Name}/{assemblyName.Version}/{moduleVersionId}/{fileInfo.Length}/{fileInfo.LastWriteTimeUtc.Ticks}";
            }
            catch (Exception exception) when (exception is BadImageFormatException or FileLoadException or FileNotFoundException)
            {
                return $"{Path.GetFileName(path.Value)}/{fileInfo.Length}/{fileInfo.LastWriteTimeUtc.Ticks}";
            }
        }
    }
}
