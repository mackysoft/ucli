namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Defines daemon session storage contract constants shared by CLI and Unity Editor. </summary>
internal static class DaemonSessionStorageContract
{
    /// <summary> Gets the current daemon session persistence schema version. </summary>
    public const int CurrentSchemaVersion = 2;

    /// <summary> Gets the maximum encoded size accepted for one <c>session.json</c> file. </summary>
    public const int MaximumFileSizeBytes = 64 * 1024;
}
