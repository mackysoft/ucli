namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;

/// <summary> Identifies an independently serialized lane for daemon-owned mutations. </summary>
internal enum DaemonOperationLane
{
    /// <summary> Mutations required to restore or transition daemon lifecycle state. </summary>
    LifecycleCompensation,

    /// <summary> Supplemental persistence that must not delay lifecycle compensation. </summary>
    SupplementalPersistence,
}
