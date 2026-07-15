using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingReadIndexArtifactReader : IReadIndexArtifactReader
{
    private readonly List<ReadInvocation> readInvocations = [];

    public IReadOnlyList<ReadInvocation> ReadInvocations => readInvocations;

    public ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot>? OpsCatalogResult { get; set; }

    public Queue<ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot>> OpsCatalogResults { get; } = [];

    public ReadIndexArtifactReadResult<OpsDescribeSnapshot>? OpsDescribeResult { get; set; }

    public Queue<ReadIndexArtifactReadResult<OpsDescribeSnapshot>> OpsDescribeResults { get; } = [];

    public ReadIndexArtifactReadResult<AssetSearchLookupSnapshot>? AssetSearchLookupResult { get; set; }

    public ReadIndexArtifactReadResult<GuidPathLookupSnapshot>? GuidPathLookupResult { get; set; }

    public ReadIndexArtifactReadResult<SceneTreeLiteLookupSnapshot>? SceneTreeLiteLookupResult { get; set; }

    public ReadIndexArtifactReadResult<ReadIndexInputsManifestSnapshot>? InputsManifestResult { get; set; }

    public ValueTask<ReadIndexArtifactReadResult<OpsCatalogDescriptorSnapshot>> ReadOpsCatalogAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return ReadAsync(
            ReadIndexArtifactKind.OpsCatalog,
            unityProject,
            OpsCatalogResults.Count == 0 ? OpsCatalogResult : OpsCatalogResults.Dequeue(),
            cancellationToken);
    }

    public ValueTask<ReadIndexArtifactReadResult<OpsDescribeSnapshot>> ReadOpsDescribeAsync (
        ResolvedUnityProjectContext unityProject,
        ValidatedOpsCatalogEntry catalogEntry,
        Sha256Digest sourceInputsHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalogEntry);
        ArgumentNullException.ThrowIfNull(sourceInputsHash);
        return ReadAsync(
            ReadIndexArtifactKind.OpsDescribe,
            unityProject,
            OpsDescribeResults.Count == 0 ? OpsDescribeResult : OpsDescribeResults.Dequeue(),
            cancellationToken);
    }

    public ValueTask<ReadIndexArtifactReadResult<AssetSearchLookupSnapshot>> ReadAssetSearchLookupAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return ReadAsync(
            ReadIndexArtifactKind.AssetSearchLookup,
            unityProject,
            AssetSearchLookupResult,
            cancellationToken);
    }

    public ValueTask<ReadIndexArtifactReadResult<GuidPathLookupSnapshot>> ReadGuidPathLookupAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return ReadAsync(
            ReadIndexArtifactKind.GuidPathLookup,
            unityProject,
            GuidPathLookupResult,
            cancellationToken);
    }

    public ValueTask<ReadIndexArtifactReadResult<SceneTreeLiteLookupSnapshot>> ReadSceneTreeLiteLookupAsync (
        ResolvedUnityProjectContext unityProject,
        SceneAssetPath scenePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenePath);
        return ReadAsync(
            ReadIndexArtifactKind.SceneTreeLiteLookup,
            unityProject,
            SceneTreeLiteLookupResult,
            cancellationToken);
    }

    public ValueTask<ReadIndexArtifactReadResult<ReadIndexInputsManifestSnapshot>> ReadInputsManifestAsync (
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
        AssetSearchLookup,
        GuidPathLookup,
        SceneTreeLiteLookup,
        InputsManifest,
    }
}
