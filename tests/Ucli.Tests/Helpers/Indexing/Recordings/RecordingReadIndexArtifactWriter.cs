using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.Tests.Helpers.Indexing;

internal sealed class RecordingReadIndexArtifactWriter : IReadIndexArtifactWriter
{
    private readonly bool allowAssetLookups;
    private readonly bool allowOpsCatalog;
    private readonly bool allowSceneTreeLite;

    private readonly List<AssetLookupInvocation> assetLookupInvocations = [];
    private readonly List<OpsCatalogInvocation> opsCatalogInvocations = [];
    private readonly List<SceneTreeLiteInvocation> sceneTreeLiteInvocations = [];

    private RecordingReadIndexArtifactWriter (
        bool allowAssetLookups,
        bool allowOpsCatalog,
        bool allowSceneTreeLite)
    {
        this.allowAssetLookups = allowAssetLookups;
        this.allowOpsCatalog = allowOpsCatalog;
        this.allowSceneTreeLite = allowSceneTreeLite;
    }

    public IReadOnlyList<AssetLookupInvocation> AssetLookupInvocations => assetLookupInvocations;

    public IReadOnlyList<OpsCatalogInvocation> OpsCatalogInvocations => opsCatalogInvocations;

    public IReadOnlyList<SceneTreeLiteInvocation> SceneTreeLiteInvocations => sceneTreeLiteInvocations;

    public Exception? WriteException { get; set; }

    public static RecordingReadIndexArtifactWriter ForAssetLookups ()
    {
        return new RecordingReadIndexArtifactWriter(
            allowAssetLookups: true,
            allowOpsCatalog: false,
            allowSceneTreeLite: false);
    }

    public static RecordingReadIndexArtifactWriter ForOpsCatalog ()
    {
        return new RecordingReadIndexArtifactWriter(
            allowAssetLookups: false,
            allowOpsCatalog: true,
            allowSceneTreeLite: false);
    }

    public static RecordingReadIndexArtifactWriter ForSceneTreeLite ()
    {
        return new RecordingReadIndexArtifactWriter(
            allowAssetLookups: false,
            allowOpsCatalog: false,
            allowSceneTreeLite: true);
    }

    public ValueTask WriteAssetLookupsAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexAssetSearchEntryJsonContract> assetSearchEntries,
        IReadOnlyList<IndexGuidPathEntryJsonContract> guidPathEntries,
        ReadIndexInputHashSnapshot inputSnapshot,
        CancellationToken cancellationToken = default)
    {
        if (!allowAssetLookups)
        {
            throw new NotSupportedException("Asset lookup writes are not expected by this test.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        assetLookupInvocations.Add(new AssetLookupInvocation(
            storageRoot,
            projectFingerprint,
            generatedAtUtc,
            assetSearchEntries,
            guidPathEntries,
            inputSnapshot,
            cancellationToken));
        if (WriteException is not null)
        {
            throw WriteException;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask WriteOpsCatalogAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract> operations,
        string sourceInputsHash,
        ReadIndexInputHashSnapshot? manifestInputSnapshot,
        CancellationToken cancellationToken = default)
    {
        if (!allowOpsCatalog)
        {
            throw new NotSupportedException("Ops catalog writes are not expected by this test.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        opsCatalogInvocations.Add(new OpsCatalogInvocation(
            storageRoot,
            projectFingerprint,
            generatedAtUtc,
            operations,
            sourceInputsHash,
            manifestInputSnapshot,
            cancellationToken));
        if (WriteException is not null)
        {
            throw WriteException;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask WriteSceneTreeLiteAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        DateTimeOffset generatedAtUtc,
        string scenePath,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots,
        string sourceInputsHash,
        CancellationToken cancellationToken = default)
    {
        if (!allowSceneTreeLite)
        {
            throw new NotSupportedException("Scene tree lite writes are not expected by this test.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        sceneTreeLiteInvocations.Add(new SceneTreeLiteInvocation(
            storageRoot,
            projectFingerprint,
            generatedAtUtc,
            scenePath,
            roots,
            sourceInputsHash,
            cancellationToken));
        if (WriteException is not null)
        {
            throw WriteException;
        }

        return ValueTask.CompletedTask;
    }

    internal readonly record struct AssetLookupInvocation (
        string StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        DateTimeOffset GeneratedAtUtc,
        IReadOnlyList<IndexAssetSearchEntryJsonContract> AssetSearchEntries,
        IReadOnlyList<IndexGuidPathEntryJsonContract> GuidPathEntries,
        ReadIndexInputHashSnapshot InputSnapshot,
        CancellationToken CancellationToken);

    internal readonly record struct OpsCatalogInvocation (
        string StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        DateTimeOffset GeneratedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract> Operations,
        string SourceInputsHash,
        ReadIndexInputHashSnapshot? ManifestInputSnapshot,
        CancellationToken CancellationToken);

    internal readonly record struct SceneTreeLiteInvocation (
        string StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        DateTimeOffset GeneratedAtUtc,
        string ScenePath,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> Roots,
        string SourceInputsHash,
        CancellationToken CancellationToken);
}
