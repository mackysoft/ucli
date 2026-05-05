namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;

/// <summary> Represents one GUID-path lookup read output. </summary>
internal sealed record GuidPathLookupReadOutput (
    IndexGuidPathEntryJsonContract? Entry,
    AssetLookupAccessInfo AccessInfo);
