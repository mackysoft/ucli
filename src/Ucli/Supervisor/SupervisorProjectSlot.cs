namespace MackySoft.Ucli.Supervisor;

/// <summary> Stores per-project synchronization and process-tracking state for the supervisor runtime. </summary>
internal sealed class SupervisorProjectSlot
{
    /// <summary> Initializes a new instance of the <see cref="SupervisorProjectSlot" /> class. </summary>
    public SupervisorProjectSlot ()
    {
        Gate = new SemaphoreSlim(1, 1);
    }

    /// <summary> Gets the exclusive gate for lifecycle operations targeting one project fingerprint. </summary>
    public SemaphoreSlim Gate { get; }

    /// <summary> Gets or sets the currently tracked managed process for this project fingerprint. </summary>
    public SupervisorManagedDaemonProcess? ManagedProcess { get; set; }

    /// <summary> Gets or sets one pending background lifecycle task for this project fingerprint. </summary>
    public Task? PendingOperation { get; set; }
}