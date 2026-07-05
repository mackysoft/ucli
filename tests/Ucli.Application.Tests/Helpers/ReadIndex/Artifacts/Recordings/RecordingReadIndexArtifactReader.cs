namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingReadIndexArtifactReader : IReadIndexArtifactReader
{
    private readonly List<ReadInvocation> readInvocations = [];

    public IReadOnlyList<ReadInvocation> ReadInvocations => readInvocations;

    public ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>? OpsCatalogResult { get; set; }

    public ReadIndexArtifactReadResult<IndexOpsDescribeJsonContract>? OpsDescribeResult { get; set; }

    public ReadIndexArtifactReadResult<IndexTypesCatalogJsonContract>? TypesCatalogResult { get; set; }

    public ReadIndexArtifactReadResult<IndexSchemasCatalogJsonContract>? SchemasCatalogResult { get; set; }

    public ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract>? AssetSearchLookupResult { get; set; }

    public ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract>? GuidPathLookupResult { get; set; }

    public ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>? SceneTreeLiteLookupResult { get; set; }

    public ReadIndexArtifactReadResult<IndexInputsManifestJsonContract>? InputsManifestResult { get; set; }

    public ValueTask<ReadIndexArtifactReadResult<IndexOpsCatalogJsonContract>> ReadOpsCatalogAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return ReadAsync(
            ReadIndexArtifactKind.OpsCatalog,
            unityProject,
            OpsCatalogResult,
            cancellationToken);
    }

    public ValueTask<ReadIndexArtifactReadResult<IndexOpsDescribeJsonContract>> ReadOpsDescribeAsync (
        ResolvedUnityProjectContext unityProject,
        IndexOpsCatalogEntryJsonContract catalogEntry,
        string sourceInputsHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalogEntry);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceInputsHash);
        return ReadAsync(
            ReadIndexArtifactKind.OpsDescribe,
            unityProject,
            OpsDescribeResult,
            cancellationToken);
    }

    public ValueTask<ReadIndexArtifactReadResult<IndexTypesCatalogJsonContract>> ReadTypesCatalogAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return ReadAsync(
            ReadIndexArtifactKind.TypesCatalog,
            unityProject,
            TypesCatalogResult,
            cancellationToken);
    }

    public ValueTask<ReadIndexArtifactReadResult<IndexSchemasCatalogJsonContract>> ReadSchemasCatalogAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return ReadAsync(
            ReadIndexArtifactKind.SchemasCatalog,
            unityProject,
            SchemasCatalogResult,
            cancellationToken);
    }

    public ValueTask<ReadIndexArtifactReadResult<IndexAssetSearchLookupJsonContract>> ReadAssetSearchLookupAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return ReadAsync(
            ReadIndexArtifactKind.AssetSearchLookup,
            unityProject,
            AssetSearchLookupResult,
            cancellationToken);
    }

    public ValueTask<ReadIndexArtifactReadResult<IndexGuidPathLookupJsonContract>> ReadGuidPathLookupAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return ReadAsync(
            ReadIndexArtifactKind.GuidPathLookup,
            unityProject,
            GuidPathLookupResult,
            cancellationToken);
    }

    public ValueTask<ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>> ReadSceneTreeLiteLookupAsync (
        ResolvedUnityProjectContext unityProject,
        string scenePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenePath);
        return ReadAsync(
            ReadIndexArtifactKind.SceneTreeLiteLookup,
            unityProject,
            SceneTreeLiteLookupResult,
            cancellationToken);
    }

    public ValueTask<ReadIndexArtifactReadResult<IndexInputsManifestJsonContract>> ReadInputsManifestAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return ReadAsync(
            ReadIndexArtifactKind.InputsManifest,
            unityProject,
            InputsManifestResult,
            cancellationToken);
    }

    private ValueTask<ReadIndexArtifactReadResult<T>> ReadAsync<T> (
        ReadIndexArtifactKind kind,
        ResolvedUnityProjectContext unityProject,
        ReadIndexArtifactReadResult<T>? result,
        CancellationToken cancellationToken)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        readInvocations.Add(new ReadInvocation(kind, unityProject, cancellationToken));

        if (result is null)
        {
            throw new InvalidOperationException($"{kind} read result is not configured.");
        }

        return ValueTask.FromResult(result);
    }

    internal readonly record struct ReadInvocation (
        ReadIndexArtifactKind Kind,
        ResolvedUnityProjectContext UnityProject,
        CancellationToken CancellationToken);

    internal enum ReadIndexArtifactKind
    {
        OpsCatalog,
        OpsDescribe,
        TypesCatalog,
        SchemasCatalog,
        AssetSearchLookup,
        GuidPathLookup,
        SceneTreeLiteLookup,
        InputsManifest,
    }
}
