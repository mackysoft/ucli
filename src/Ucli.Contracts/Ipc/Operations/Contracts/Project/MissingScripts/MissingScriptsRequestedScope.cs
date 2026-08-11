using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("The requested missing script scan scope.")]
public sealed record MissingScriptsRequestedScope
{
    [JsonConstructor]
    public MissingScriptsRequestedScope (
        IReadOnlyList<UnityAssetPathPrefix> roots,
        IReadOnlyList<MissingScriptsAssetKind> assetKinds)
    {
        Roots = ContractArgumentGuard.RequireItems(roots, nameof(roots));
        if (Roots.Count == 0)
        {
            throw new ArgumentException("Missing script roots must not be empty.", nameof(roots));
        }

        AssetKinds = RequireAssetKinds(assetKinds, nameof(assetKinds));
    }

    [JsonInclude]
    [JsonRequired]
    [ItemCount(1, int.MaxValue)]
    public IReadOnlyList<UnityAssetPathPrefix> Roots { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [ItemCount(1, int.MaxValue)]
    public IReadOnlyList<MissingScriptsAssetKind> AssetKinds { get; private init; }

    private static IReadOnlyList<MissingScriptsAssetKind> RequireAssetKinds (
        IReadOnlyList<MissingScriptsAssetKind>? assetKinds,
        string parameterName)
    {
        if (assetKinds == null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (assetKinds.Count == 0)
        {
            throw new ArgumentException("Missing script asset kinds must not be empty.", parameterName);
        }

        var copy = new MissingScriptsAssetKind[assetKinds.Count];
        var seen = new HashSet<MissingScriptsAssetKind>();
        for (var i = 0; i < assetKinds.Count; i++)
        {
            if (!TextVocabulary.IsDefined(assetKinds[i]))
            {
                throw new ArgumentOutOfRangeException(parameterName, assetKinds[i], "Missing script asset kinds must be scene or prefab.");
            }

            if (!seen.Add(assetKinds[i]))
            {
                throw new ArgumentException("Missing script asset kinds must not contain duplicates.", parameterName);
            }

            copy[i] = assetKinds[i];
        }

        return Array.AsReadOnly(copy);
    }
}
