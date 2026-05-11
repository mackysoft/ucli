using System;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents persisted GUI supervisor endpoint metadata for CLI rebootstrap requests. </summary>
    internal sealed record UnityGuiSupervisorManifest (
        int SchemaVersion,
        string SessionToken,
        string ProjectFingerprint,
        string EndpointTransportKind,
        string EndpointAddress,
        int ProcessId,
        DateTimeOffset? ProcessStartedAtUtc,
        DateTimeOffset IssuedAtUtc)
    {
        public const int CurrentSchemaVersion = 1;
    }
}
