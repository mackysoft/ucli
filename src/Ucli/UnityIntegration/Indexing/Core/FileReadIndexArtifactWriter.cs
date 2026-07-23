using System.Text;
using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Core;

/// <summary> Persists read-index artifacts to local storage paths. </summary>
internal sealed class FileReadIndexArtifactWriter : IReadIndexArtifactWriter
{
    private const int SchemaVersion = 1;

    private const int MaxUnreferencedOpsDescribeArtifactCount = 512;

    private static readonly TimeSpan WriteLockAcquireTimeout = TimeSpan.FromSeconds(1);

    private readonly IJsonContractWriter<IndexOpsCatalogJsonContract> opsCatalogWriter;

    private readonly IJsonContractWriter<IndexOpsDescribeJsonContract> opsDescribeWriter;

    private readonly IJsonContractWriter<IndexAssetSearchLookupJsonContract> assetSearchLookupWriter;

    private readonly IJsonContractWriter<IndexGuidPathLookupJsonContract> guidPathLookupWriter;

    private readonly IJsonContractWriter<IndexSceneTreeLiteLookupJsonContract> sceneTreeLiteLookupWriter;

    private readonly IJsonContractWriter<IndexInputsManifestJsonContract> inputsManifestWriter;

    private readonly FileReadIndexGenerationStore generationStore;

    /// <summary> Initializes a new instance of the <see cref="FileReadIndexArtifactWriter" /> class. </summary>
    public FileReadIndexArtifactWriter (
        IJsonContractWriter<IndexOpsCatalogJsonContract> opsCatalogWriter,
        IJsonContractWriter<IndexOpsDescribeJsonContract> opsDescribeWriter,
        IJsonContractWriter<IndexAssetSearchLookupJsonContract> assetSearchLookupWriter,
        IJsonContractWriter<IndexGuidPathLookupJsonContract> guidPathLookupWriter,
        IJsonContractWriter<IndexSceneTreeLiteLookupJsonContract> sceneTreeLiteLookupWriter,
        IJsonContractWriter<IndexInputsManifestJsonContract> inputsManifestWriter,
        FileReadIndexGenerationStore generationStore)
    {
        this.opsCatalogWriter = opsCatalogWriter ?? throw new ArgumentNullException(nameof(opsCatalogWriter));
        this.opsDescribeWriter = opsDescribeWriter ?? throw new ArgumentNullException(nameof(opsDescribeWriter));
        this.assetSearchLookupWriter = assetSearchLookupWriter ?? throw new ArgumentNullException(nameof(assetSearchLookupWriter));
        this.guidPathLookupWriter = guidPathLookupWriter ?? throw new ArgumentNullException(nameof(guidPathLookupWriter));
        this.sceneTreeLiteLookupWriter = sceneTreeLiteLookupWriter ?? throw new ArgumentNullException(nameof(sceneTreeLiteLookupWriter));
        this.inputsManifestWriter = inputsManifestWriter ?? throw new ArgumentNullException(nameof(inputsManifestWriter));
        this.generationStore = generationStore ?? throw new ArgumentNullException(nameof(generationStore));
    }

    /// <inheritdoc />
    public async ValueTask WriteOpsCatalogAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<ValidatedOpsOperation> operations,
        Sha256Digest sourceInputsHash,
        ReadIndexInputHashSnapshot? manifestInputSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storageRoot);
        ArgumentNullException.ThrowIfNull(projectFingerprint);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(sourceInputsHash);
        cancellationToken.ThrowIfCancellationRequested();

        using var generation = await generationStore.BeginWriteAsync(
                storageRoot,
                projectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (manifestInputSnapshot != null)
        {
            manifestInputSnapshot = await PreserveCurrentAssetHashesAsync(
                    generation.StagingDirectoryPath,
                    manifestInputSnapshot,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var opsCatalogPath = ResolveChild(
            generation.StagingDirectoryPath,
            UcliStoragePathNames.OpsCatalogFileName);
        var opsDescribeDirectoryPath = UcliStoragePathResolver.ResolveOpsDescribeDirectory(storageRoot, projectFingerprint);
        FileSystemAccessBoundary.EnsureSecureDirectory(opsDescribeDirectoryPath);

        var operationContracts = new IndexOpEntryJsonContract[operations.Count];
        for (var i = 0; i < operations.Count; i++)
        {
            operationContracts[i] = operations[i].ToJsonContract();
        }

        var orderedOperations = IndexJsonOrderingPolicy.OrderOpsEntries(operationContracts);
        var catalogEntries = new List<IndexOpsCatalogEntryJsonContract>(orderedOperations.Count);
        var referencedDescribePaths = new HashSet<AbsolutePath>();
        for (var i = 0; i < orderedOperations.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var operation = orderedOperations[i];
            var describeContract = new IndexOpsDescribeJsonContract(
                SchemaVersion: SchemaVersion,
                GeneratedAtUtc: generatedAtUtc,
                SourceInputsHash: sourceInputsHash.ToString(),
                Operation: operation);
            var describeJson = opsDescribeWriter.Write(describeContract);
            var describeHash = Sha256Digest.Compute(Encoding.UTF8.GetBytes(describeJson));
            var describePath = UcliStoragePathResolver.ResolveOpsDescribePath(
                storageRoot,
                projectFingerprint,
                describeHash);

            await FileUtilities.WriteAllTextAtomicallyAsync(
                    describePath,
                    describeJson,
                    cancellationToken)
                .ConfigureAwait(false);
            referencedDescribePaths.Add(describePath);

            catalogEntries.Add(
                new IndexOpsCatalogEntryJsonContract(
                    Name: operation.Name,
                    Kind: operation.Kind,
                    Policy: operation.Policy,
                    Description: operation.Description,
                    DescribeKey: describeHash.ToString(),
                    DescribeHash: describeHash.ToString()));
        }

        var opsCatalog = new IndexOpsCatalogJsonContract(
            SchemaVersion: SchemaVersion,
            GeneratedAtUtc: generatedAtUtc,
            SourceInputsHash: sourceInputsHash.ToString(),
            Entries: catalogEntries);

        await FileUtilities.WriteAllTextAtomicallyAsync(
                opsCatalogPath,
                opsCatalogWriter.Write(opsCatalog),
                cancellationToken)
            .ConfigureAwait(false);

        if (manifestInputSnapshot != null)
        {
            await WriteInputsManifestAsync(
                    generation.StagingDirectoryPath,
                    generatedAtUtc,
                    manifestInputSnapshot,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await ValidateGenerationAsync(
                generation.StagingDirectoryPath,
                storageRoot,
                projectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        await generation.CommitAsync(cancellationToken).ConfigureAwait(false);
        if (TryAddRetainedGenerationDescribePaths(
                storageRoot,
                projectFingerprint,
                referencedDescribePaths))
        {
            PruneOpsDescribeArtifacts(opsDescribeDirectoryPath, referencedDescribePaths);
        }
    }

    /// <inheritdoc />
    public async ValueTask WriteAssetLookupsAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexAssetSearchEntryJsonContract> assetSearchEntries,
        IReadOnlyList<IndexGuidPathEntryJsonContract> guidPathEntries,
        ReadIndexInputHashSnapshot inputSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storageRoot);
        ArgumentNullException.ThrowIfNull(assetSearchEntries);
        ArgumentNullException.ThrowIfNull(guidPathEntries);
        ArgumentNullException.ThrowIfNull(inputSnapshot);
        cancellationToken.ThrowIfCancellationRequested();

        using var generation = await generationStore.BeginWriteAsync(
                storageRoot,
                projectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        inputSnapshot = await PreserveCurrentCoreHashesAsync(
                generation.StagingDirectoryPath,
                inputSnapshot,
                cancellationToken)
            .ConfigureAwait(false);

        var assetSearchLookupPath = ResolveChild(
            generation.StagingDirectoryPath,
            UcliStoragePathNames.AssetSearchLookupFileName);
        var guidPathLookupPath = ResolveChild(
            generation.StagingDirectoryPath,
            UcliStoragePathNames.GuidPathLookupFileName);

        var assetSearchLookup = new IndexAssetSearchLookupJsonContract(
            SchemaVersion: SchemaVersion,
            GeneratedAtUtc: generatedAtUtc,
            SourceInputsHash: inputSnapshot.AssetSearchHash.ToString(),
            Entries: assetSearchEntries);
        var guidPathLookup = new IndexGuidPathLookupJsonContract(
            SchemaVersion: SchemaVersion,
            GeneratedAtUtc: generatedAtUtc,
            SourceInputsHash: inputSnapshot.GuidPathHash.ToString(),
            Entries: guidPathEntries);

        await FileUtilities.WriteAllTextAtomicallyAsync(
                assetSearchLookupPath,
                assetSearchLookupWriter.Write(assetSearchLookup),
                cancellationToken)
            .ConfigureAwait(false);
        await FileUtilities.WriteAllTextAtomicallyAsync(
                guidPathLookupPath,
                guidPathLookupWriter.Write(guidPathLookup),
                cancellationToken)
            .ConfigureAwait(false);
        await WriteInputsManifestAsync(
                generation.StagingDirectoryPath,
                generatedAtUtc,
                inputSnapshot,
                cancellationToken)
            .ConfigureAwait(false);
        await ValidateGenerationAsync(
                generation.StagingDirectoryPath,
                storageRoot,
                projectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        await generation.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask WriteSceneTreeLiteAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        DateTimeOffset generatedAtUtc,
        SceneAssetPath scenePath,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots,
        Sha256Digest sourceInputsHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storageRoot);
        ArgumentNullException.ThrowIfNull(projectFingerprint);
        ArgumentNullException.ThrowIfNull(scenePath);
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(sourceInputsHash);
        cancellationToken.ThrowIfCancellationRequested();

        using var writeLock = await FileExclusiveLock.AcquireAsync(
                UcliStoragePathResolver.ResolveReadIndexWriteLockPath(storageRoot, projectFingerprint),
                WriteLockAcquireTimeout,
                cancellationToken)
            .ConfigureAwait(false);

        var lookupPath = UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(storageRoot, projectFingerprint, scenePath);
        EnsureParentDirectory(lookupPath);

        var lookup = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: SchemaVersion,
            GeneratedAtUtc: generatedAtUtc,
            ScenePath: scenePath.Value,
            SourceInputsHash: sourceInputsHash.ToString(),
            Roots: roots);

        await FileUtilities.WriteAllTextAtomicallyAsync(
                lookupPath,
                sceneTreeLiteLookupWriter.Write(lookup),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static void PruneOpsDescribeArtifacts (
        AbsolutePath describeDirectoryPath,
        HashSet<AbsolutePath> referencedPaths)
    {
        try
        {
            if (!IsRegularDirectory(describeDirectoryPath))
            {
                return;
            }

            var unreferencedArtifacts = Directory
                .EnumerateFiles(describeDirectoryPath.Value, "*.json", SearchOption.TopDirectoryOnly)
                .Select(path => TryGetOwnedOpsDescribeArtifact(describeDirectoryPath, path))
                .Where(static artifact => artifact.HasValue)
                .Select(static artifact => artifact!.Value)
                .Where(artifact => !referencedPaths.Contains(artifact.Path))
                .OrderByDescending(static artifact => artifact.LastWriteTimeUtc)
                .Skip(MaxUnreferencedOpsDescribeArtifactCount)
                .ToArray();

            foreach (var artifact in unreferencedArtifacts)
            {
                try
                {
                    if (IsOwnedOpsDescribeArtifact(artifact.Path))
                    {
                        File.Delete(artifact.Path.Value);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // NOTE: Pruning is best-effort housekeeping after the new catalog has committed.
                    // A sharing violation must not turn a valid read-index refresh into a failure.
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // NOTE: Keep the current catalog usable when directory enumeration races with cleanup.
        }
    }

    private static OpsDescribeArtifact? TryGetOwnedOpsDescribeArtifact (
        AbsolutePath describeDirectoryPath,
        string rawPath)
    {
        if (!AbsolutePath.TryParse(rawPath, out var path, out _)
            || !ContainedPath.TryCreate(describeDirectoryPath, path, out var containedPath, out _)
            || !IsOwnedOpsDescribeArtifact(containedPath.Target))
        {
            return null;
        }

        return new OpsDescribeArtifact(
            containedPath.Target,
            new FileInfo(containedPath.Target.Value).LastWriteTimeUtc);
    }

    private static bool IsOwnedOpsDescribeArtifact (AbsolutePath path)
    {
        try
        {
            if (!string.Equals(
                    Path.GetExtension(path.Value),
                    UcliStoragePathNames.OpsDescribeFileExtension,
                    StringComparison.Ordinal))
            {
                return false;
            }

            var fileName = Path.GetFileNameWithoutExtension(path.Value);
            if (!StoragePathSegmentCodec.IsEncodedSha256Digest(fileName))
            {
                return false;
            }

            FileUtilities.EnsureRegularFile(path, "Read-index operation description");
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryAddRetainedGenerationDescribePaths (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        HashSet<AbsolutePath> referencedPaths)
    {
        try
        {
            var generationRoot = UcliStoragePathResolver.ResolveReadIndexGenerationsDirectory(
                storageRoot,
                projectFingerprint);
            foreach (var rawGenerationDirectoryPath in Directory.EnumerateDirectories(
                         generationRoot.Value,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                if (!AbsolutePath.TryParse(rawGenerationDirectoryPath, out var generationDirectoryPath, out _)
                    || !ContainedPath.TryCreate(generationRoot, generationDirectoryPath, out var containedGenerationDirectoryPath, out _)
                    || !IsRegularDirectory(containedGenerationDirectoryPath.Target))
                {
                    return false;
                }

                var catalogJson = FileUtilities.ReadAllTextOrNull(
                    ResolveChild(containedGenerationDirectoryPath.Target, UcliStoragePathNames.OpsCatalogFileName));
                if (catalogJson == null)
                {
                    continue;
                }

                if (!OpsCatalogDescriptorSnapshot.TryCreate(
                        IndexOpsCatalogJsonContractSerializer.Deserialize(catalogJson),
                        out var catalog))
                {
                    return false;
                }

                for (var index = 0; index < catalog.Entries.Count; index++)
                {
                    referencedPaths.Add(UcliStoragePathResolver.ResolveOpsDescribePath(
                        storageRoot,
                        projectFingerprint,
                        catalog.Entries[index].DescribeKey));
                }
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or JsonException)
        {
            // NOTE: Keep all content-addressed details when retained-generation references cannot be proven.
            return false;
        }
    }

    private static async ValueTask<ReadIndexInputHashSnapshot> PreserveCurrentAssetHashesAsync (
        AbsolutePath generationDirectoryPath,
        ReadIndexInputHashSnapshot suppliedSnapshot,
        CancellationToken cancellationToken)
    {
        var currentSnapshot = await TryReadCurrentInputsManifestAsync(
                generationDirectoryPath,
                cancellationToken)
            .ConfigureAwait(false);
        if (currentSnapshot == null)
        {
            return suppliedSnapshot;
        }

        // The operation-catalog refresh computes core hashes before waiting for this lock. Preserve asset hashes
        // committed by a lookup writer that completed while this operation was waiting.
        return suppliedSnapshot.WithAssetHashes(
            currentSnapshot.AssetsContentHash,
            currentSnapshot.AssetSearchHash,
            currentSnapshot.GuidPathHash);
    }

    private static async ValueTask<ReadIndexInputHashSnapshot> PreserveCurrentCoreHashesAsync (
        AbsolutePath generationDirectoryPath,
        ReadIndexInputHashSnapshot suppliedSnapshot,
        CancellationToken cancellationToken)
    {
        var currentSnapshot = await TryReadCurrentInputsManifestAsync(
                generationDirectoryPath,
                cancellationToken)
            .ConfigureAwait(false);
        if (currentSnapshot == null)
        {
            return suppliedSnapshot;
        }

        // The asset lookup refresh computes a full snapshot before waiting for this lock. Preserve core hashes
        // committed by an operation-catalog writer that completed while this operation was waiting.
        return suppliedSnapshot.WithCoreHashes(currentSnapshot);
    }

    private static async ValueTask<ReadIndexInputHashSnapshot?> TryReadCurrentInputsManifestAsync (
        AbsolutePath generationDirectoryPath,
        CancellationToken cancellationToken)
    {
        var inputsManifestPath = ResolveChild(
            generationDirectoryPath,
            UcliStoragePathNames.IndexInputsManifestFileName);
        string? json;
        try
        {
            json = await FileUtilities.ReadAllTextOrNullAsync(inputsManifestPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if (json == null)
        {
            return null;
        }

        IndexInputsManifestJsonContract? currentManifest;
        try
        {
            currentManifest = IndexInputsManifestJsonContractSerializer.Deserialize(json);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException)
        {
            return null;
        }

        if (!ReadIndexInputsManifestSnapshot.TryCreate(currentManifest, out var manifestSnapshot))
        {
            return null;
        }

        return manifestSnapshot.Hashes;
    }

    private async ValueTask WriteInputsManifestAsync (
        AbsolutePath generationDirectoryPath,
        DateTimeOffset generatedAtUtc,
        ReadIndexInputHashSnapshot inputSnapshot,
        CancellationToken cancellationToken)
    {
        var inputsManifestPath = ResolveChild(
            generationDirectoryPath,
            UcliStoragePathNames.IndexInputsManifestFileName);

        var inputsManifest = new IndexInputsManifestJsonContract(
            SchemaVersion: SchemaVersion,
            GeneratedAtUtc: generatedAtUtc,
            ScriptAssembliesHash: inputSnapshot.ScriptAssembliesHash.ToString(),
            PackagesManifestHash: inputSnapshot.PackagesManifestHash.ToString(),
            PackagesLockHash: inputSnapshot.PackagesLockHash.ToString(),
            AssemblyDefinitionHash: inputSnapshot.AssemblyDefinitionHash.ToString(),
            AssetsContentHash: inputSnapshot.AssetsContentHash.ToString(),
            AssetSearchHash: inputSnapshot.AssetSearchHash.ToString(),
            GuidPathHash: inputSnapshot.GuidPathHash.ToString(),
            CombinedHash: inputSnapshot.CombinedHash.ToString());

        await FileUtilities.WriteAllTextAtomicallyAsync(
                inputsManifestPath,
                inputsManifestWriter.Write(inputsManifest),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask ValidateGenerationAsync (
        AbsolutePath generationDirectoryPath,
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken)
    {
        var manifestJson = await FileUtilities.ReadAllTextOrNullAsync(
                ResolveChild(generationDirectoryPath, UcliStoragePathNames.IndexInputsManifestFileName),
                cancellationToken)
            .ConfigureAwait(false);
        ReadIndexInputsManifestSnapshot? manifest = null;
        if (manifestJson != null
            && !ReadIndexInputsManifestSnapshot.TryCreate(
                IndexInputsManifestJsonContractSerializer.Deserialize(manifestJson),
                out manifest))
        {
            throw new InvalidDataException("The staged read-index manifest is malformed.");
        }

        var catalogJson = await FileUtilities.ReadAllTextOrNullAsync(
                ResolveChild(generationDirectoryPath, UcliStoragePathNames.OpsCatalogFileName),
                cancellationToken)
            .ConfigureAwait(false);
        OpsCatalogDescriptorSnapshot? catalog = null;
        if (catalogJson != null)
        {
            if (!OpsCatalogDescriptorSnapshot.TryCreate(
                    IndexOpsCatalogJsonContractSerializer.Deserialize(catalogJson),
                    out catalog))
            {
                throw new InvalidDataException("The staged read-index operation catalog is malformed.");
            }

            for (var index = 0; index < catalog.Entries.Count; index++)
            {
                await ValidateDescribeAsync(
                        storageRoot,
                        projectFingerprint,
                        catalog,
                        catalog.Entries[index],
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var assetSearchJson = await FileUtilities.ReadAllTextOrNullAsync(
                ResolveChild(generationDirectoryPath, UcliStoragePathNames.AssetSearchLookupFileName),
                cancellationToken)
            .ConfigureAwait(false);
        var guidPathJson = await FileUtilities.ReadAllTextOrNullAsync(
                ResolveChild(generationDirectoryPath, UcliStoragePathNames.GuidPathLookupFileName),
                cancellationToken)
            .ConfigureAwait(false);
        if ((assetSearchJson == null) != (guidPathJson == null))
        {
            throw new InvalidDataException("The staged read-index asset lookup set is incomplete.");
        }

        AssetSearchLookupSnapshot? assetSearch = null;
        GuidPathLookupSnapshot? guidPath = null;
        if (assetSearchJson != null
            && (!AssetSearchLookupSnapshot.TryCreate(
                    IndexAssetSearchLookupJsonContractSerializer.Deserialize(assetSearchJson),
                    out assetSearch)
                || !GuidPathLookupSnapshot.TryCreate(
                    IndexGuidPathLookupJsonContractSerializer.Deserialize(guidPathJson!),
                    out guidPath)))
        {
            throw new InvalidDataException("The staged read-index asset lookup set is malformed.");
        }

        if (assetSearch != null
            && guidPath != null
            && (assetSearch.GeneratedAtUtc != guidPath.GeneratedAtUtc
                || !AssetLookupSnapshot.TryCreate(
                    assetSearch.GeneratedAtUtc,
                    assetSearch.Entries,
                    guidPath.Entries,
                    out _,
                    out _)))
        {
            throw new InvalidDataException("The staged read-index asset lookup set is inconsistent.");
        }

        if (manifest != null
            && ((catalog != null && catalog.SourceInputsHash != manifest.Hashes.CombinedHash)
                || (assetSearch != null && assetSearch.SourceInputsHash != manifest.Hashes.AssetSearchHash)
                || (guidPath != null && guidPath.SourceInputsHash != manifest.Hashes.GuidPathHash)))
        {
            throw new InvalidDataException("The staged read-index artifacts do not match the inputs manifest.");
        }
    }

    private static async ValueTask ValidateDescribeAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        OpsCatalogDescriptorSnapshot catalog,
        ValidatedOpsCatalogEntry entry,
        CancellationToken cancellationToken)
    {
        var describePath = UcliStoragePathResolver.ResolveOpsDescribePath(
            storageRoot,
            projectFingerprint,
            entry.DescribeKey);
        var describeJson = await FileUtilities.ReadAllTextOrNullAsync(describePath, cancellationToken).ConfigureAwait(false);
        if (describeJson == null
            || Sha256Digest.Compute(Encoding.UTF8.GetBytes(describeJson)) != entry.DescribeHash
            || !OpsDescribeSnapshot.TryCreate(
                IndexOpsDescribeJsonContractSerializer.Deserialize(describeJson),
                out var describe)
            || describe.GeneratedAtUtc != catalog.GeneratedAtUtc
            || describe.SourceInputsHash != catalog.SourceInputsHash
            || !string.Equals(describe.Operation.Name, entry.Name, StringComparison.Ordinal)
            || describe.Operation.Kind != entry.Kind
            || describe.Operation.Policy != entry.Policy
            || !string.Equals(describe.Operation.Description, entry.Description, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"The staged operation description is malformed: {entry.Name}");
        }
    }

    private static void EnsureParentDirectory (AbsolutePath filePath)
    {
        if (!filePath.TryGetParent(out var directoryPath))
        {
            throw new InvalidOperationException($"Directory path could not be resolved: {filePath.Value}");
        }

        FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
    }

    private static AbsolutePath ResolveChild (
        AbsolutePath directoryPath,
        string fileName)
    {
        return ContainedPath.Create(
            directoryPath,
            RootRelativePath.Parse(fileName)).Target;
    }

    private static bool IsRegularDirectory (AbsolutePath directoryPath)
    {
        if (!Directory.Exists(directoryPath.Value))
        {
            return false;
        }

        var attributes = File.GetAttributes(directoryPath.Value);
        return (attributes & FileAttributes.Directory) != 0
            && (attributes & FileAttributes.ReparsePoint) == 0;
    }

    private readonly record struct OpsDescribeArtifact (
        AbsolutePath Path,
        DateTime LastWriteTimeUtc);
}
