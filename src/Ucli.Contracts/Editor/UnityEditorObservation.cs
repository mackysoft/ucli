using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Editor;

/// <summary> Represents one provider-normalized Unity Editor lifecycle observation. </summary>
public sealed record UnityEditorObservation
{
    /// <summary> Initializes one provider-normalized Unity Editor observation. </summary>
    /// <param name="serverVersion"> The non-empty execution-provider server version. </param>
    /// <param name="unityVersion"> The non-empty Unity Editor version. </param>
    /// <param name="projectFingerprint"> The Unity project fingerprint served by the execution provider. </param>
    /// <param name="state"> The comparable Unity Editor state. </param>
    /// <param name="observedAtUtc"> The non-default observation timestamp. </param>
    /// <param name="actionRequired"> The optional action required to resolve the lifecycle blocker. </param>
    /// <param name="primaryDiagnostic"> The optional primary lifecycle diagnostic. </param>
    /// <exception cref="ArgumentException"> Thrown when a required version has no content or <paramref name="observedAtUtc" /> is not a non-default UTC timestamp. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="projectFingerprint" /> or <paramref name="state" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="actionRequired" /> is undefined. </exception>
    [JsonConstructor]
    public UnityEditorObservation (
        string serverVersion,
        string unityVersion,
        ProjectFingerprint projectFingerprint,
        UnityEditorStateSnapshot state,
        DateTimeOffset observedAtUtc,
        UnityEditorActionRequired? actionRequired,
        UnityEditorPrimaryDiagnostic? primaryDiagnostic)
    {
        if (string.IsNullOrWhiteSpace(serverVersion))
        {
            throw new ArgumentException("Server version must not be empty.", nameof(serverVersion));
        }

        if (string.IsNullOrWhiteSpace(unityVersion))
        {
            throw new ArgumentException("Unity version must not be empty.", nameof(unityVersion));
        }

        if (actionRequired.HasValue && !TextVocabulary.IsDefined(actionRequired.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(actionRequired), actionRequired, "Unsupported Unity Editor recovery action.");
        }

        ServerVersion = serverVersion;
        UnityVersion = unityVersion;
        ProjectFingerprint = ContractArgumentGuard.RequireNotNull(projectFingerprint, nameof(projectFingerprint));
        State = state ?? throw new ArgumentNullException(nameof(state));
        ObservedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(observedAtUtc, nameof(observedAtUtc));
        ActionRequired = actionRequired;
        PrimaryDiagnostic = primaryDiagnostic;
    }

    /// <summary> Gets the execution-provider server version string. </summary>
    public string ServerVersion { get; }

    /// <summary> Gets the Unity Editor version. </summary>
    public string UnityVersion { get; }

    /// <summary> Gets the Unity project fingerprint served by the execution provider. </summary>
    public ProjectFingerprint ProjectFingerprint { get; }

    /// <summary> Gets the comparable Unity Editor state observed by the execution provider. </summary>
    [JsonInclude]
    [JsonRequired]
    public UnityEditorStateSnapshot State { get; private init; }

    /// <summary> Gets the UTC timestamp when lifecycle values were observed. </summary>
    public DateTimeOffset ObservedAtUtc { get; }

    /// <summary> Gets the normalized action required to resolve the current lifecycle state. </summary>
    public UnityEditorActionRequired? ActionRequired { get; }

    /// <summary> Gets the primary machine-readable diagnostic for the current lifecycle state. </summary>
    public UnityEditorPrimaryDiagnostic? PrimaryDiagnostic { get; }
}
