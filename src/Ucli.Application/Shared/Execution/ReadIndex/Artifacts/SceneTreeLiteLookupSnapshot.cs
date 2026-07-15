using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Represents one validated persisted scene-tree-lite lookup snapshot. </summary>
internal sealed record SceneTreeLiteLookupSnapshot : IReadIndexArtifactSnapshot
{
    private const int SupportedSchemaVersion = 1;

    private SceneTreeLiteLookupSnapshot (
        DateTimeOffset generatedAtUtc,
        Sha256Digest sourceInputsHash,
        SceneAssetPath scenePath,
        IReadOnlyList<SceneTreeLiteNode> roots)
    {
        GeneratedAtUtc = generatedAtUtc;
        SourceInputsHash = sourceInputsHash;
        ScenePath = scenePath;
        Roots = roots;
    }

    /// <inheritdoc />
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <inheritdoc />
    public Sha256Digest SourceInputsHash { get; }

    /// <summary> Gets the normalized scene path represented by the lookup. </summary>
    public SceneAssetPath ScenePath { get; }

    /// <summary> Gets the validated scene-tree root nodes. </summary>
    public IReadOnlyList<SceneTreeLiteNode> Roots { get; }

    /// <summary> Projects a persisted scene-tree-lite contract when its values are valid. </summary>
    public static bool TryCreate (
        IndexSceneTreeLiteLookupJsonContract? contract,
        [NotNullWhen(true)]
        out SceneTreeLiteLookupSnapshot? snapshot)
    {
        snapshot = null;
        if (contract == null
            || contract.SchemaVersion != SupportedSchemaVersion
            || contract.GeneratedAtUtc == default
            || contract.GeneratedAtUtc.Offset != TimeSpan.Zero
            || !Sha256Digest.TryParse(contract.SourceInputsHash, out var sourceInputsHash)
            || !SceneAssetPath.TryParse(contract.ScenePath, out var scenePath)
            || !IndexCatalogContractValidator.TryProjectSceneTreeLiteNodes(
                contract.Roots,
                "roots",
                out var roots,
                out _))
        {
            return false;
        }

        snapshot = new SceneTreeLiteLookupSnapshot(
            contract.GeneratedAtUtc,
            sourceInputsHash,
            scenePath,
            roots);
        return true;
    }
}
