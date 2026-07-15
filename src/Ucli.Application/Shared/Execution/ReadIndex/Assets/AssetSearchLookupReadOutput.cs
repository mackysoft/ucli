namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;

/// <summary> Represents one asset-search lookup read output. </summary>
internal sealed record AssetSearchLookupReadOutput
{
    public AssetSearchLookupReadOutput (
        IReadOnlyList<AssetSearchLookupEntry> Entries,
        AssetLookupAccessInfo AccessInfo)
    {
        ArgumentNullException.ThrowIfNull(Entries);
        this.Entries = Array.AsReadOnly(Entries.ToArray());
        this.AccessInfo = AccessInfo ?? throw new ArgumentNullException(nameof(AccessInfo));
    }

    public IReadOnlyList<AssetSearchLookupEntry> Entries { get; }

    public AssetLookupAccessInfo AccessInfo { get; }
}
