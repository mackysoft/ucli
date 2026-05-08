namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Classifies persisted ops-catalog read failures for access-policy decisions. </summary>
internal enum PersistedOpsCatalogReadFailureKind
{
    /// <summary> Indicates that the resolved persisted artifact path inputs are invalid. </summary>
    InvalidArgument = 0,

    /// <summary> Indicates that the persisted catalog is unavailable or could not be read. </summary>
    Unavailable = 1,

    /// <summary> Indicates that the persisted catalog exists but violates its contract. </summary>
    Malformed = 2,

    /// <summary> Indicates that freshness could not be observed for a loaded persisted catalog. </summary>
    FreshnessUnavailable = 3,
}
