using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("Missing script check result.")]
public sealed record MissingScriptsCheckResult
{
    [JsonConstructor]
    public MissingScriptsCheckResult (
        MissingScriptsRequestedScope requestedScope,
        IReadOnlyList<MissingScriptsUnscannedScope> unscannedScopes,
        IReadOnlyList<UnityAssetPath> scannedAssets,
        IReadOnlyList<MissingScriptsUnscannedAsset> unscannedAssets,
        IReadOnlyList<MissingScriptSlot> missingScriptSlots)
    {
        RequestedScope = requestedScope ?? throw new ArgumentNullException(nameof(requestedScope));
        UnscannedScopes = ContractArgumentGuard.RequireItems(unscannedScopes, nameof(unscannedScopes));
        ScannedAssets = ContractArgumentGuard.RequireItems(scannedAssets, nameof(scannedAssets));
        UnscannedAssets = ContractArgumentGuard.RequireItems(unscannedAssets, nameof(unscannedAssets));
        MissingScriptSlots = ContractArgumentGuard.RequireItems(missingScriptSlots, nameof(missingScriptSlots));
    }

    [JsonInclude]
    [JsonRequired]
    public MissingScriptsRequestedScope RequestedScope { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public IReadOnlyList<MissingScriptsUnscannedScope> UnscannedScopes { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public IReadOnlyList<UnityAssetPath> ScannedAssets { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public IReadOnlyList<MissingScriptsUnscannedAsset> UnscannedAssets { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public IReadOnlyList<MissingScriptSlot> MissingScriptSlots { get; private init; }
}
