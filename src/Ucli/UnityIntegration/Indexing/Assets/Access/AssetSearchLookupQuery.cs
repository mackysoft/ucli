namespace MackySoft.Ucli.UnityIntegration.Indexing.Assets.Access;

/// <summary> Represents one assets.find-style lookup query executed against persisted asset search entries. </summary>
internal sealed record AssetSearchLookupQuery (
    string? TypeId,
    string? PathPrefix,
    string? NameContains);