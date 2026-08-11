using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("An asset that was discovered but could not be scanned for missing script slots.")]
public sealed record MissingScriptsUnscannedAsset
{
    [JsonConstructor]
    public MissingScriptsUnscannedAsset (
        UnityAssetPath assetPath,
        MissingScriptsAssetKind assetKind,
        MissingScriptsUnscannedReason reason)
    {
        if (!TextVocabulary.IsDefined(assetKind))
        {
            throw new ArgumentOutOfRangeException(nameof(assetKind), assetKind, "Missing script asset kind must be defined.");
        }

        if (reason is not MissingScriptsUnscannedReason.AssetChanged
            and not MissingScriptsUnscannedReason.AssetReadFailed
            and not MissingScriptsUnscannedReason.HierarchyPathUnrepresentable)
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "An unscanned asset must report an asset-specific reason.");
        }

        AssetPath = assetPath ?? throw new ArgumentNullException(nameof(assetPath));
        AssetKind = assetKind;
        Reason = reason;
    }

    [JsonInclude]
    [JsonRequired]
    public UnityAssetPath AssetPath { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public MissingScriptsAssetKind AssetKind { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public MissingScriptsUnscannedReason Reason { get; private init; }
}
