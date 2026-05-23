namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines timing contracts shared by lifecycle sidecar writers and readers. </summary>
public static class DaemonLifecycleObservationTimings
{
    private const int FreshnessRefreshSlotCount = 5;

    // NOTE:
    // These values define the sidecar observation contract between Unity and the CLI. They are freshness limits,
    // not identity evidence; readers still require the same session and matching live process before trusting a file.

    /// <summary> Gets the minimum interval between periodic lifecycle sidecar writes. </summary>
    public static TimeSpan SidecarRefreshInterval { get; } = TimeSpan.FromSeconds(1);

    /// <summary> Gets the maximum age accepted when a sidecar substitutes for an unavailable IPC response. </summary>
    public static TimeSpan FreshnessWindow { get; } = TimeSpan.FromTicks(
        SidecarRefreshInterval.Ticks * FreshnessRefreshSlotCount);
}
