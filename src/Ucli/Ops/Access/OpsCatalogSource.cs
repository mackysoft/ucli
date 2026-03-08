namespace MackySoft.Ucli.Ops.Access;

/// <summary> Defines internal source classifications used by ops catalog access. </summary>
internal enum OpsCatalogSource
{
    /// <summary> Indicates that the catalog was obtained from persisted read-index. </summary>
    Index = 0,

    /// <summary> Indicates that the catalog was obtained from the fallback source path. </summary>
    Source = 1,
}