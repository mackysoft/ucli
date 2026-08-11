using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("A requested asset scope that could not be enumerated.")]
public sealed record MissingScriptsUnscannedScope
{
    [JsonConstructor]
    public MissingScriptsUnscannedScope (
        UnityAssetPathPrefix root,
        MissingScriptsAssetKind assetKind,
        MissingScriptsUnscannedReason reason)
    {
        if (!TextVocabulary.IsDefined(assetKind))
        {
            throw new ArgumentOutOfRangeException(nameof(assetKind), assetKind, "Missing script asset kind must be defined.");
        }

        if (reason != MissingScriptsUnscannedReason.ScopeReadFailed)
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "An unscanned scope must report scopeReadFailed.");
        }

        Root = root ?? throw new ArgumentNullException(nameof(root));
        AssetKind = assetKind;
        Reason = reason;
    }

    [JsonInclude]
    [JsonRequired]
    public UnityAssetPathPrefix Root { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public MissingScriptsAssetKind AssetKind { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public MissingScriptsUnscannedReason Reason { get; private init; }
}
