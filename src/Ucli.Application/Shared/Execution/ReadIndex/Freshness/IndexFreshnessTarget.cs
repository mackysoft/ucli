namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Identifies one read-index artifact whose freshness is evaluated against current project inputs. </summary>
internal enum IndexFreshnessTarget
{
    OpsCatalog,
    AssetSearchLookup,
    GuidPathLookup,
}
