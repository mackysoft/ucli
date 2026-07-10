using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Infrastructure.Paths;
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

    /// <summary> Initializes a new instance of the <see cref="FileReadIndexArtifactWriter" /> class. </summary>
    public FileReadIndexArtifactWriter (
        IJsonContractWriter<IndexOpsCatalogJsonContract> opsCatalogWriter,
        IJsonContractWriter<IndexOpsDescribeJsonContract> opsDescribeWriter,
        IJsonContractWriter<IndexAssetSearchLookupJsonContract> assetSearchLookupWriter,
        IJsonContractWriter<IndexGuidPathLookupJsonContract> guidPathLookupWriter,
        IJsonContractWriter<IndexSceneTreeLiteLookupJsonContract> sceneTreeLiteLookupWriter,
        IJsonContractWriter<IndexInputsManifestJsonContract> inputsManifestWriter)
    {
        this.opsCatalogWriter = opsCatalogWriter ?? throw new ArgumentNullException(nameof(opsCatalogWriter));
        this.opsDescribeWriter = opsDescribeWriter ?? throw new ArgumentNullException(nameof(opsDescribeWriter));
        this.assetSearchLookupWriter = assetSearchLookupWriter ?? throw new ArgumentNullException(nameof(assetSearchLookupWriter));
        this.guidPathLookupWriter = guidPathLookupWriter ?? throw new ArgumentNullException(nameof(guidPathLookupWriter));
        this.sceneTreeLiteLookupWriter = sceneTreeLiteLookupWriter ?? throw new ArgumentNullException(nameof(sceneTreeLiteLookupWriter));
        this.inputsManifestWriter = inputsManifestWriter ?? throw new ArgumentNullException(nameof(inputsManifestWriter));
    }

    /// <inheritdoc />
    public async ValueTask WriteOpsCatalogAsync (
        string storageRoot,
        string projectFingerprint,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract> operations,
        string sourceInputsHash,
        ReadIndexInputHashSnapshot? manifestInputSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFingerprint);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceInputsHash);
        cancellationToken.ThrowIfCancellationRequested();

        using var writeLock = await FileExclusiveLock.AcquireAsync(
                UcliStoragePathResolver.ResolveReadIndexWriteLockPath(storageRoot, projectFingerprint),
                WriteLockAcquireTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (manifestInputSnapshot != null)
        {
            manifestInputSnapshot = await PreserveCurrentAssetHashesAsync(
                    storageRoot,
                    projectFingerprint,
                    manifestInputSnapshot,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var opsCatalogPath = UcliStoragePathResolver.ResolveOpsCatalogPath(storageRoot, projectFingerprint);
        var opsDescribeDirectoryPath = UcliStoragePathResolver.ResolveOpsDescribeDirectory(storageRoot, projectFingerprint);
        EnsureParentDirectory(opsCatalogPath);
        FileSystemAccessBoundary.EnsureSecureDirectory(opsDescribeDirectoryPath);

        var orderedOperations = IndexJsonOrderingPolicy.OrderOpsEntries(operations);
        var catalogEntries = new List<IndexOpsCatalogEntryJsonContract>(orderedOperations.Count);
        var originalDescribeArtifacts = new Dictionary<string, string?>(StringComparer.Ordinal);
        var catalogWritten = false;
        try
        {
            for (var i = 0; i < orderedOperations.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var operation = orderedOperations[i];
                if (string.IsNullOrWhiteSpace(operation.Name))
                {
                    throw new InvalidOperationException("Operation name must not be empty when writing ops read-index artifacts.");
                }

                var describeContract = new IndexOpsDescribeJsonContract(
                    SchemaVersion: SchemaVersion,
                    GeneratedAtUtc: generatedAtUtc,
                    SourceInputsHash: sourceInputsHash,
                    Operation: operation);
                var describeJson = opsDescribeWriter.Write(describeContract);
                var describeHash = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(describeJson));
                var describeKey = describeHash;
                var describePath = UcliStoragePathResolver.ResolveOpsDescribePath(
                    storageRoot,
                    projectFingerprint,
                    describeKey);

                await CaptureOriginalArtifactAsync(describePath, originalDescribeArtifacts, cancellationToken).ConfigureAwait(false);
                await FileUtilities.WriteAllTextAtomicallyAsync(
                        describePath,
                        describeJson,
                        cancellationToken)
                    .ConfigureAwait(false);

                catalogEntries.Add(
                    new IndexOpsCatalogEntryJsonContract(
                        Name: operation.Name,
                        Kind: operation.Kind,
                        Policy: operation.Policy,
                        Description: operation.Description,
                        DescribeKey: describeKey,
                        DescribeHash: describeHash));
            }

            var opsCatalog = new IndexOpsCatalogJsonContract(
                SchemaVersion: SchemaVersion,
                GeneratedAtUtc: generatedAtUtc,
                SourceInputsHash: sourceInputsHash,
                Entries: catalogEntries);

            await FileUtilities.WriteAllTextAtomicallyAsync(
                    opsCatalogPath,
                    opsCatalogWriter.Write(opsCatalog),
                    cancellationToken)
                .ConfigureAwait(false);
            catalogWritten = true;
        }
        catch
        {
            if (!catalogWritten)
            {
                await RestoreArtifactsAsync(originalDescribeArtifacts).ConfigureAwait(false);
            }

            throw;
        }

        if (manifestInputSnapshot != null)
        {
            await WriteInputsManifestAsync(
                    storageRoot,
                    projectFingerprint,
                    generatedAtUtc,
                    manifestInputSnapshot,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        PruneOpsDescribeArtifacts(opsDescribeDirectoryPath, catalogEntries);
    }

    /// <inheritdoc />
    public async ValueTask WriteAssetLookupsAsync (
        string storageRoot,
        string projectFingerprint,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexAssetSearchEntryJsonContract> assetSearchEntries,
        IReadOnlyList<IndexGuidPathEntryJsonContract> guidPathEntries,
        ReadIndexInputHashSnapshot inputSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFingerprint);
        ArgumentNullException.ThrowIfNull(assetSearchEntries);
        ArgumentNullException.ThrowIfNull(guidPathEntries);
        ArgumentNullException.ThrowIfNull(inputSnapshot);
        cancellationToken.ThrowIfCancellationRequested();

        using var writeLock = await FileExclusiveLock.AcquireAsync(
                UcliStoragePathResolver.ResolveReadIndexWriteLockPath(storageRoot, projectFingerprint),
                WriteLockAcquireTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        inputSnapshot = await PreserveCurrentCoreHashesAsync(
                storageRoot,
                projectFingerprint,
                inputSnapshot,
                cancellationToken)
            .ConfigureAwait(false);

        var assetSearchLookupPath = UcliStoragePathResolver.ResolveAssetSearchLookupPath(storageRoot, projectFingerprint);
        var guidPathLookupPath = UcliStoragePathResolver.ResolveGuidPathLookupPath(storageRoot, projectFingerprint);
        var inputsManifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(storageRoot, projectFingerprint);
        EnsureParentDirectory(assetSearchLookupPath);
        EnsureParentDirectory(guidPathLookupPath);
        EnsureParentDirectory(inputsManifestPath);

        var assetSearchLookup = new IndexAssetSearchLookupJsonContract(
            SchemaVersion: SchemaVersion,
            GeneratedAtUtc: generatedAtUtc,
            SourceInputsHash: inputSnapshot.AssetSearchHash,
            Entries: assetSearchEntries);
        var guidPathLookup = new IndexGuidPathLookupJsonContract(
            SchemaVersion: SchemaVersion,
            GeneratedAtUtc: generatedAtUtc,
            SourceInputsHash: inputSnapshot.GuidPathHash,
            Entries: guidPathEntries);

        var originalArtifacts = new Dictionary<string, string?>(StringComparer.Ordinal);
        await CaptureOriginalArtifactAsync(assetSearchLookupPath, originalArtifacts, cancellationToken).ConfigureAwait(false);
        await CaptureOriginalArtifactAsync(guidPathLookupPath, originalArtifacts, cancellationToken).ConfigureAwait(false);
        await CaptureOriginalArtifactAsync(inputsManifestPath, originalArtifacts, cancellationToken).ConfigureAwait(false);
        var manifestWritten = false;
        try
        {
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
            await WriteInputsManifestAsync(storageRoot, projectFingerprint, generatedAtUtc, inputSnapshot, cancellationToken).ConfigureAwait(false);
            manifestWritten = true;
        }
        catch
        {
            if (!manifestWritten)
            {
                await RestoreArtifactsAsync(originalArtifacts).ConfigureAwait(false);
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask WriteSceneTreeLiteAsync (
        string storageRoot,
        string projectFingerprint,
        DateTimeOffset generatedAtUtc,
        string scenePath,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots,
        string sourceInputsHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenePath);
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceInputsHash);
        cancellationToken.ThrowIfCancellationRequested();

        using var writeLock = await FileExclusiveLock.AcquireAsync(
                UcliStoragePathResolver.ResolveReadIndexWriteLockPath(storageRoot, projectFingerprint),
                WriteLockAcquireTimeout,
                cancellationToken)
            .ConfigureAwait(false);

        var normalizedScenePath = PathStringNormalizer.ToSlashSeparated(scenePath);
        var lookupPath = UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(storageRoot, projectFingerprint, normalizedScenePath);
        EnsureParentDirectory(lookupPath);

        var lookup = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: SchemaVersion,
            GeneratedAtUtc: generatedAtUtc,
            ScenePath: normalizedScenePath,
            SourceInputsHash: sourceInputsHash,
            Roots: roots);

        await FileUtilities.WriteAllTextAtomicallyAsync(
                lookupPath,
                sceneTreeLiteLookupWriter.Write(lookup),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask CaptureOriginalArtifactAsync (
        string artifactPath,
        Dictionary<string, string?> originalArtifacts,
        CancellationToken cancellationToken)
    {
        if (originalArtifacts.ContainsKey(artifactPath))
        {
            return;
        }

        var originalJson = await FileUtilities.ReadAllTextOrNullAsync(artifactPath, cancellationToken).ConfigureAwait(false);
        originalArtifacts.Add(artifactPath, originalJson);
    }

    private static async ValueTask RestoreArtifactsAsync (Dictionary<string, string?> originalArtifacts)
    {
        foreach (var artifact in originalArtifacts)
        {
            if (artifact.Value == null)
            {
                FileUtilities.DeleteIfExists(artifact.Key);
                continue;
            }

            await FileUtilities.WriteAllTextAtomicallyAsync(
                    artifact.Key,
                    artifact.Value,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private static void PruneOpsDescribeArtifacts (
        string describeDirectoryPath,
        IReadOnlyList<IndexOpsCatalogEntryJsonContract> currentEntries)
    {
        try
        {
            var referencedPaths = currentEntries
                .Where(static entry => !string.IsNullOrWhiteSpace(entry.DescribeKey))
                .Select(entry => Path.Combine(describeDirectoryPath, $"{entry.DescribeKey}.json"))
                .ToHashSet(StringComparer.Ordinal);
            var unreferencedArtifacts = Directory
                .EnumerateFiles(describeDirectoryPath, "*.json", SearchOption.TopDirectoryOnly)
                .Where(path => !referencedPaths.Contains(path))
                .Select(path => new FileInfo(path))
                .OrderByDescending(static file => file.LastWriteTimeUtc)
                .Skip(MaxUnreferencedOpsDescribeArtifactCount)
                .ToArray();

            foreach (var artifact in unreferencedArtifacts)
            {
                try
                {
                    FileUtilities.DeleteIfExists(artifact.FullName);
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

    private static async ValueTask<ReadIndexInputHashSnapshot> PreserveCurrentAssetHashesAsync (
        string storageRoot,
        string projectFingerprint,
        ReadIndexInputHashSnapshot suppliedSnapshot,
        CancellationToken cancellationToken)
    {
        var currentManifest = await TryReadCurrentInputsManifestAsync(
                storageRoot,
                projectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (currentManifest == null)
        {
            return suppliedSnapshot;
        }

        // The operation-catalog refresh computes core hashes before waiting for this lock. Preserve asset hashes
        // committed by a lookup writer that completed while this operation was waiting.
        return suppliedSnapshot with
        {
            AssetsContentHash = currentManifest.AssetsContentHash!,
            AssetSearchHash = currentManifest.AssetSearchHash!,
            GuidPathHash = currentManifest.GuidPathHash!,
        };
    }

    private static async ValueTask<ReadIndexInputHashSnapshot> PreserveCurrentCoreHashesAsync (
        string storageRoot,
        string projectFingerprint,
        ReadIndexInputHashSnapshot suppliedSnapshot,
        CancellationToken cancellationToken)
    {
        var currentManifest = await TryReadCurrentInputsManifestAsync(
                storageRoot,
                projectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (currentManifest == null)
        {
            return suppliedSnapshot;
        }

        // The asset lookup refresh computes a full snapshot before waiting for this lock. Preserve core hashes
        // committed by an operation-catalog writer that completed while this operation was waiting.
        return suppliedSnapshot with
        {
            ScriptAssembliesHash = currentManifest.ScriptAssembliesHash!,
            PackagesManifestHash = currentManifest.PackagesManifestHash!,
            PackagesLockHash = currentManifest.PackagesLockHash,
            AssemblyDefinitionHash = currentManifest.AssemblyDefinitionHash!,
            CombinedHash = currentManifest.CombinedHash!,
        };
    }

    private static async ValueTask<IndexInputsManifestJsonContract?> TryReadCurrentInputsManifestAsync (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken)
    {
        var inputsManifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(storageRoot, projectFingerprint);
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

        return currentManifest != null && IndexCatalogContractValidator.IsValidInputsManifest(currentManifest)
            ? currentManifest
            : null;
    }

    private async ValueTask WriteInputsManifestAsync (
        string storageRoot,
        string projectFingerprint,
        DateTimeOffset generatedAtUtc,
        ReadIndexInputHashSnapshot inputSnapshot,
        CancellationToken cancellationToken)
    {
        var inputsManifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(storageRoot, projectFingerprint);
        EnsureParentDirectory(inputsManifestPath);

        var inputsManifest = new IndexInputsManifestJsonContract(
            SchemaVersion: SchemaVersion,
            GeneratedAtUtc: generatedAtUtc,
            ScriptAssembliesHash: inputSnapshot.ScriptAssembliesHash,
            PackagesManifestHash: inputSnapshot.PackagesManifestHash,
            PackagesLockHash: inputSnapshot.PackagesLockHash,
            AssemblyDefinitionHash: inputSnapshot.AssemblyDefinitionHash,
            AssetsContentHash: inputSnapshot.AssetsContentHash,
            AssetSearchHash: inputSnapshot.AssetSearchHash,
            GuidPathHash: inputSnapshot.GuidPathHash,
            CombinedHash: inputSnapshot.CombinedHash);

        await FileUtilities.WriteAllTextAtomicallyAsync(
                inputsManifestPath,
                inputsManifestWriter.Write(inputsManifest),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static void EnsureParentDirectory (string filePath)
    {
        var directoryPath = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {filePath}");
        FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
    }
}
