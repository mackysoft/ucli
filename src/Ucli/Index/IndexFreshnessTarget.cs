namespace MackySoft.Ucli.Index;

/// <summary> Identifies one read-index artifact whose freshness is evaluated against current project inputs. </summary>
internal enum IndexFreshnessTarget
{
    OpsCatalog,
    TypesCatalog,
    SchemasCatalog,
    AssetSearchLookup,
    GuidPathLookup,
}