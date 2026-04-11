using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Assets.Access;

/// <summary> Represents one GUID-path lookup read output. </summary>
internal sealed record GuidPathLookupReadOutput (
    IndexGuidPathEntryJsonContract? Entry,
    AssetLookupAccessInfo AccessInfo);