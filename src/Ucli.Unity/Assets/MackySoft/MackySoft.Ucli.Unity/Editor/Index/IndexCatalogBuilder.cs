using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Index;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Builds types/schema catalogs and input manifest contracts for read-index persistence. </summary>
    internal sealed class IndexCatalogBuilder : IIndexCatalogBuilder
    {
        private const int ContractSchemaVersion = 1;

        private readonly IComponentSchemaExtractor componentSchemaExtractor;
        private readonly IAssetSchemaExtractor assetSchemaExtractor;
        private readonly IIndexInputFingerprintCalculator inputFingerprintCalculator;

        /// <summary> Initializes a new instance of the <see cref="IndexCatalogBuilder" /> class. </summary>
        /// <param name="componentSchemaExtractor"> The component schema extractor dependency. </param>
        /// <param name="assetSchemaExtractor"> The asset schema extractor dependency. </param>
        /// <param name="inputFingerprintCalculator"> The input fingerprint calculator dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
        public IndexCatalogBuilder (
            IComponentSchemaExtractor componentSchemaExtractor,
            IAssetSchemaExtractor assetSchemaExtractor,
            IIndexInputFingerprintCalculator inputFingerprintCalculator)
        {
            this.componentSchemaExtractor = componentSchemaExtractor ?? throw new ArgumentNullException(nameof(componentSchemaExtractor));
            this.assetSchemaExtractor = assetSchemaExtractor ?? throw new ArgumentNullException(nameof(assetSchemaExtractor));
            this.inputFingerprintCalculator = inputFingerprintCalculator ?? throw new ArgumentNullException(nameof(inputFingerprintCalculator));
        }

        /// <summary> Builds one full index catalog snapshot for one project root path. </summary>
        /// <param name="projectRootPath"> The Unity project root path. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The build result. </returns>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="projectRootPath" /> is <see langword="null" />, empty, or whitespace. </exception>
        public async ValueTask<IndexCatalogBuildResult> Build (
            string projectRootPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(projectRootPath))
            {
                throw new ArgumentException("Project root path must not be empty.", nameof(projectRootPath));
            }

            var inputSnapshot = await inputFingerprintCalculator.TryCompute(projectRootPath, cancellationToken);
            if (inputSnapshot == null)
            {
                return IndexCatalogBuildResult.Failure("Failed to compute index input snapshot.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var projectAssemblyNames = ResolveProjectAssemblyNames();
            var componentTypes = ResolveComponentTypes(projectAssemblyNames);
            var assetTypes = ResolveAssetTypes(projectAssemblyNames);

            cancellationToken.ThrowIfCancellationRequested();
            var componentSchemaResult = await componentSchemaExtractor.Extract(componentTypes, cancellationToken);
            var assetSchemaResult = await assetSchemaExtractor.Extract(assetTypes, cancellationToken);
            var schemaEntries = componentSchemaResult.Entries
                .Concat(assetSchemaResult.Entries)
                .OrderBy(static entry => entry.SchemaKey ?? string.Empty, StringComparer.Ordinal)
                .ToArray();

            cancellationToken.ThrowIfCancellationRequested();
            var catalogTypes = BuildCatalogTypes(
                projectAssemblyNames,
                componentTypes,
                assetTypes,
                componentSchemaResult.ReferencedTypes,
                assetSchemaResult.ReferencedTypes);
            var typeEntries = catalogTypes
                .Select(CreateTypeEntry)
                .OrderBy(static entry => entry.TypeId ?? string.Empty, StringComparer.Ordinal)
                .ToArray();

            cancellationToken.ThrowIfCancellationRequested();
            var generatedAtUtc = DateTimeOffset.UtcNow;
            var sourceInputsHash = inputSnapshot.CombinedHash;
            var typesCatalog = new IndexTypesCatalogJsonContract(
                SchemaVersion: ContractSchemaVersion,
                GeneratedAtUtc: generatedAtUtc,
                SourceInputsHash: sourceInputsHash,
                Entries: typeEntries);
            var schemasCatalog = new IndexSchemasCatalogJsonContract(
                SchemaVersion: ContractSchemaVersion,
                GeneratedAtUtc: generatedAtUtc,
                SourceInputsHash: sourceInputsHash,
                Entries: schemaEntries);
            var inputsManifest = new IndexInputsManifestJsonContract(
                SchemaVersion: ContractSchemaVersion,
                GeneratedAtUtc: generatedAtUtc,
                ScriptAssembliesHash: inputSnapshot.ScriptAssembliesHash,
                PackagesManifestHash: inputSnapshot.PackagesManifestHash,
                PackagesLockHash: inputSnapshot.PackagesLockHash,
                AssemblyDefinitionHash: inputSnapshot.AssemblyDefinitionHash,
                CombinedHash: inputSnapshot.CombinedHash);

            return IndexCatalogBuildResult.Success(typesCatalog, schemasCatalog, inputsManifest);
        }

        private static IReadOnlyCollection<Type> BuildCatalogTypes (
            HashSet<string> projectAssemblyNames,
            IReadOnlyList<Type> componentTypes,
            IReadOnlyList<Type> assetTypes,
            IReadOnlyCollection<Type> componentReferencedTypes,
            IReadOnlyCollection<Type> assetReferencedTypes)
        {
            var types = new HashSet<Type>();
            AddTypes(types, componentTypes);
            AddTypes(types, assetTypes);
            AddTypes(types, componentReferencedTypes);
            AddTypes(types, assetReferencedTypes);
            AddTypes(types, ResolveSerializeReferenceCandidateTypes(projectAssemblyNames));

            return types
                .OrderBy(static type => IndexTypeIdFormatter.Format(type), StringComparer.Ordinal)
                .ToArray();
        }

        private static void AddTypes (
            HashSet<Type> destination,
            IEnumerable<Type> source)
        {
            foreach (var type in source)
            {
                if (IsCatalogType(type))
                {
                    destination.Add(type);
                }
            }
        }

        private static bool IsCatalogType (Type type)
        {
            return type != null
                && !type.IsPointer
                && !type.IsByRef
                && !type.ContainsGenericParameters;
        }

        private static HashSet<string> ResolveProjectAssemblyNames ()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assembly in CompilationPipeline.GetAssemblies())
            {
                if (!string.IsNullOrWhiteSpace(assembly.name))
                {
                    names.Add(assembly.name);
                }
            }

            return names;
        }

        private static IReadOnlyList<Type> ResolveComponentTypes (HashSet<string> projectAssemblyNames)
        {
            var componentTypes = new List<Type>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<Component>())
            {
                if (IsProjectSchemaRootType(type, projectAssemblyNames))
                {
                    componentTypes.Add(type);
                }
            }

            componentTypes.Sort(static (x, y) =>
                StringComparer.Ordinal.Compare(IndexTypeIdFormatter.Format(x), IndexTypeIdFormatter.Format(y)));
            return componentTypes;
        }

        private static IReadOnlyList<Type> ResolveAssetTypes (HashSet<string> projectAssemblyNames)
        {
            var assetTypes = new List<Type>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
            {
                if (IsProjectSchemaRootType(type, projectAssemblyNames))
                {
                    assetTypes.Add(type);
                }
            }

            assetTypes.Sort(static (x, y) =>
                StringComparer.Ordinal.Compare(IndexTypeIdFormatter.Format(x), IndexTypeIdFormatter.Format(y)));
            return assetTypes;
        }

        private static IReadOnlyList<Type> ResolveSerializeReferenceCandidateTypes (HashSet<string> projectAssemblyNames)
        {
            var candidateTypes = new List<Type>();
            foreach (var type in TypeCache.GetTypesWithAttribute<SerializableAttribute>())
            {
                if (IsProjectAssemblyType(type, projectAssemblyNames) && IsSerializeReferenceCandidate(type))
                {
                    candidateTypes.Add(type);
                }
            }

            candidateTypes.Sort(static (x, y) =>
                StringComparer.Ordinal.Compare(IndexTypeIdFormatter.Format(x), IndexTypeIdFormatter.Format(y)));
            return candidateTypes;
        }

        private static IndexTypeEntryJsonContract CreateTypeEntry (Type type)
        {
            var assemblyName = type.Assembly.GetName().Name ?? "unknown";
            var baseTypeId = type.BaseType == null
                ? null
                : IndexTypeIdFormatter.Format(type.BaseType);
            return new IndexTypeEntryJsonContract(
                TypeId: IndexTypeIdFormatter.Format(type),
                DisplayName: type.Name,
                Namespace: type.Namespace,
                AssemblyName: assemblyName,
                BaseTypeId: baseTypeId,
                Flags: new IndexTypeFlagsJsonContract(
                    IsAbstract: type.IsAbstract,
                    IsGenericDefinition: type.IsGenericTypeDefinition,
                    IsUnityObject: typeof(UnityEngine.Object).IsAssignableFrom(type),
                    IsComponent: typeof(Component).IsAssignableFrom(type),
                    IsScriptableObject: typeof(ScriptableObject).IsAssignableFrom(type),
                    IsSerializeReferenceCandidate: IsSerializeReferenceCandidate(type)));
        }

        private static bool IsProjectSchemaRootType (
            Type type,
            HashSet<string> projectAssemblyNames)
        {
            return IsProjectAssemblyType(type, projectAssemblyNames)
                && !type.IsAbstract
                && !type.IsGenericTypeDefinition
                && !type.ContainsGenericParameters;
        }

        private static bool IsProjectAssemblyType (
            Type type,
            HashSet<string> projectAssemblyNames)
        {
            if (type == null)
            {
                return false;
            }

            var assemblyName = type.Assembly.GetName().Name;
            return !string.IsNullOrWhiteSpace(assemblyName) && projectAssemblyNames.Contains(assemblyName);
        }

        private static bool IsSerializeReferenceCandidate (Type type)
        {
            return type != null
                && type.IsClass
                && !type.IsAbstract
                && !type.IsGenericTypeDefinition
                && !type.ContainsGenericParameters
                && type.IsDefined(typeof(SerializableAttribute), inherit: false)
                && !typeof(UnityEngine.Object).IsAssignableFrom(type);
        }
    }
}