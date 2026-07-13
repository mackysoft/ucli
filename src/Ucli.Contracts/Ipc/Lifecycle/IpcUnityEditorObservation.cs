using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one Unity Editor lifecycle observation exposed by the IPC protocol. </summary>
public sealed record IpcUnityEditorObservation
{
    /// <summary> Initializes one Unity Editor observation exposed by the IPC protocol. </summary>
    [JsonConstructor]
    public IpcUnityEditorObservation (
        string serverVersion,
        string unityVersion,
        string projectFingerprint,
        UnityEditorStateSnapshot state,
        DateTimeOffset observedAtUtc,
        string? actionRequired = null,
        IpcPrimaryDiagnostic? primaryDiagnostic = null)
    {
        if (string.IsNullOrWhiteSpace(serverVersion))
        {
            throw new ArgumentException("Server version must not be empty.", nameof(serverVersion));
        }

        if (string.IsNullOrWhiteSpace(unityVersion))
        {
            throw new ArgumentException("Unity version must not be empty.", nameof(unityVersion));
        }

        if (string.IsNullOrWhiteSpace(projectFingerprint))
        {
            throw new ArgumentException("Project fingerprint must not be empty.", nameof(projectFingerprint));
        }

        if (observedAtUtc == default)
        {
            throw new ArgumentOutOfRangeException(
                nameof(observedAtUtc),
                observedAtUtc,
                "Observation timestamp must be specified.");
        }

        ServerVersion = serverVersion;
        UnityVersion = unityVersion;
        ProjectFingerprint = projectFingerprint;
        State = state ?? throw new ArgumentNullException(nameof(state));
        ObservedAtUtc = observedAtUtc;
        ActionRequired = actionRequired;
        PrimaryDiagnostic = primaryDiagnostic;
    }

    /// <summary> Gets the daemon server version string. </summary>
    public string ServerVersion { get; }

    /// <summary> Gets the Unity Editor version. </summary>
    public string UnityVersion { get; }

    /// <summary> Gets the Unity project fingerprint served by the IPC host. </summary>
    public string ProjectFingerprint { get; }

    /// <summary> Gets the comparable Unity Editor state observed by the IPC host. </summary>
    [JsonInclude]
    [JsonRequired]
    public UnityEditorStateSnapshot State { get; private init; }

    /// <summary> Gets the UTC timestamp when lifecycle values were observed. </summary>
    public DateTimeOffset ObservedAtUtc { get; }

    /// <summary> Gets the normalized action required to resolve the current lifecycle state. </summary>
    public string? ActionRequired { get; }

    /// <summary> Gets the primary machine-readable diagnostic for the current lifecycle state. </summary>
    public IpcPrimaryDiagnostic? PrimaryDiagnostic { get; }
}
