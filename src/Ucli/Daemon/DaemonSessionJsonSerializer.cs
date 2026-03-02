using System.Text.Json;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements JSON conversion for daemon session persistence models. </summary>
internal sealed class DaemonSessionJsonSerializer : IDaemonSessionSerializer
{
    /// <summary> Deserializes daemon session JSON text to model. </summary>
    /// <param name="json"> The daemon session JSON text. </param>
    /// <returns> The deserialized daemon session model. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="json" /> is empty. </exception>
    /// <exception cref="JsonException"> Thrown when JSON schema is invalid. </exception>
    public DaemonSession Deserialize (string json)
    {
        var contract = DaemonSessionJsonContractSerializer.Deserialize(json)
            ?? throw new JsonException("Daemon session JSON is null.");

        return new DaemonSession(
            SchemaVersion: contract.SchemaVersion,
            SessionToken: contract.SessionToken?.Trim() ?? string.Empty,
            ProjectFingerprint: contract.ProjectFingerprint?.Trim() ?? string.Empty,
            IssuedAtUtc: contract.IssuedAtUtc,
            RuntimeKind: contract.RuntimeKind?.Trim() ?? string.Empty,
            OwnerKind: contract.OwnerKind?.Trim() ?? string.Empty,
            CanShutdownProcess: contract.CanShutdownProcess,
            EndpointTransportKind: contract.EndpointTransportKind?.Trim() ?? string.Empty,
            EndpointAddress: contract.EndpointAddress?.Trim() ?? string.Empty,
            ProcessId: contract.ProcessId);
    }

    /// <summary> Serializes daemon session model to JSON text. </summary>
    /// <param name="session"> The daemon session model. </param>
    /// <returns> The serialized daemon session JSON text. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public string Serialize (DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var contract = new DaemonSessionJsonContract(
            SchemaVersion: session.SchemaVersion,
            SessionToken: session.SessionToken,
            ProjectFingerprint: session.ProjectFingerprint,
            IssuedAtUtc: session.IssuedAtUtc,
            RuntimeKind: session.RuntimeKind,
            OwnerKind: session.OwnerKind,
            CanShutdownProcess: session.CanShutdownProcess,
            EndpointTransportKind: session.EndpointTransportKind,
            EndpointAddress: session.EndpointAddress,
            ProcessId: session.ProcessId);

        return DaemonSessionJsonContractSerializer.Serialize(contract);
    }
}