using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one Unity Editor lifecycle observation exposed by the IPC protocol. </summary>
public sealed record IpcUnityEditorObservation
{
    /// <summary> Initializes one Unity Editor observation exposed by the IPC protocol. </summary>
    /// <param name="serverVersion"> The non-empty daemon server version. </param>
    /// <param name="unityVersion"> The non-empty Unity Editor version. </param>
    /// <param name="projectFingerprint"> The Unity project fingerprint served by the IPC host. </param>
    /// <param name="state"> The comparable Unity Editor state. </param>
    /// <param name="observedAtUtc"> The non-default observation timestamp. </param>
    /// <param name="actionRequired"> The optional action required to resolve the lifecycle blocker. </param>
    /// <param name="primaryDiagnostic"> The optional primary lifecycle diagnostic. </param>
    /// <exception cref="ArgumentException"> Thrown when a required version has no content or <paramref name="observedAtUtc" /> is not a non-default UTC timestamp. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="projectFingerprint" /> or <paramref name="state" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="actionRequired" /> is undefined. </exception>
    [JsonConstructor]
    public IpcUnityEditorObservation (
        string serverVersion,
        string unityVersion,
        ProjectFingerprint projectFingerprint,
        UnityEditorStateSnapshot state,
        DateTimeOffset observedAtUtc,
        DaemonDiagnosisActionRequired? actionRequired,
        IpcPrimaryDiagnostic? primaryDiagnostic)
    {
        if (string.IsNullOrWhiteSpace(serverVersion))
        {
            throw new ArgumentException("Server version must not be empty.", nameof(serverVersion));
        }

        if (string.IsNullOrWhiteSpace(unityVersion))
        {
            throw new ArgumentException("Unity version must not be empty.", nameof(unityVersion));
        }

        if (actionRequired.HasValue && !ContractLiteralCodec.IsDefined(actionRequired.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(actionRequired), actionRequired, "Unsupported daemon diagnosis action.");
        }

        ServerVersion = serverVersion;
        UnityVersion = unityVersion;
        ProjectFingerprint = ContractArgumentGuard.RequireNotNull(projectFingerprint, nameof(projectFingerprint));
        State = state ?? throw new ArgumentNullException(nameof(state));
        ObservedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(observedAtUtc, nameof(observedAtUtc));
        ActionRequired = actionRequired;
        PrimaryDiagnostic = primaryDiagnostic;
    }

    /// <summary> Gets the daemon server version string. </summary>
    public string ServerVersion { get; }

    /// <summary> Gets the Unity Editor version. </summary>
    public string UnityVersion { get; }

    /// <summary> Gets the Unity project fingerprint served by the IPC host. </summary>
    public ProjectFingerprint ProjectFingerprint { get; }

    /// <summary> Gets the comparable Unity Editor state observed by the IPC host. </summary>
    [JsonInclude]
    [JsonRequired]
    public UnityEditorStateSnapshot State { get; private init; }

    /// <summary> Gets the UTC timestamp when lifecycle values were observed. </summary>
    public DateTimeOffset ObservedAtUtc { get; }

    /// <summary> Gets the normalized action required to resolve the current lifecycle state. </summary>
    public DaemonDiagnosisActionRequired? ActionRequired { get; }

    /// <summary> Gets the primary machine-readable diagnostic for the current lifecycle state. </summary>
    public IpcPrimaryDiagnostic? PrimaryDiagnostic { get; }
}
