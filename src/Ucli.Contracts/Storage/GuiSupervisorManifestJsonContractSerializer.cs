using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Provides shared serializer settings for GUI supervisor manifest contracts. </summary>
internal static class GuiSupervisorManifestJsonContractSerializer
{
    /// <summary> Deserializes GUI supervisor manifest JSON text to contract. </summary>
    /// <param name="json"> The GUI supervisor manifest JSON text. </param>
    /// <returns> The deserialized contract; or <see langword="null" /> when JSON root is <c>null</c>. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="json" /> is <see langword="null" />, empty, or whitespace. </exception>
    /// <exception cref="JsonException"> Thrown when JSON schema is invalid. </exception>
    public static GuiSupervisorManifestJsonContract? Deserialize (string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON text must not be empty.", nameof(json));
        }

        var persistedContract = JsonSerializer.Deserialize<PersistedContract>(
            json,
            DaemonStorageJsonSerializerOptions.Deserialize);
        if (persistedContract == null)
        {
            return null;
        }

        if (!IpcSessionToken.TryParse(persistedContract.SessionToken, out var sessionToken))
        {
            throw new JsonException("GUI supervisor manifest sessionToken is invalid.");
        }

        if (!ContractLiteralCodec.TryParse<IpcTransportKind>(
                persistedContract.EndpointTransportKind,
                out var transportKind))
        {
            throw new JsonException("GUI supervisor manifest endpointTransportKind is invalid.");
        }

        IpcEndpoint endpoint;
        try
        {
            endpoint = new IpcEndpoint(transportKind, persistedContract.EndpointAddress!);
        }
        catch (ArgumentException exception)
        {
            throw new JsonException("GUI supervisor manifest endpointAddress is invalid.", exception);
        }

        if (persistedContract.ProjectFingerprint == null)
        {
            throw new JsonException("GUI supervisor manifest projectFingerprint is missing.");
        }

        try
        {
            return new GuiSupervisorManifestJsonContract(
                persistedContract.SchemaVersion,
                sessionToken,
                persistedContract.ProjectFingerprint,
                endpoint,
                persistedContract.ProcessId,
                persistedContract.ProcessStartedAtUtc,
                persistedContract.IssuedAtUtc);
        }
        catch (ArgumentException exception)
        {
            throw new JsonException("GUI supervisor manifest contains invalid metadata.", exception);
        }
    }

    /// <summary> Serializes GUI supervisor manifest contract to JSON text. </summary>
    /// <param name="contract"> The GUI supervisor manifest contract. </param>
    /// <returns> The serialized GUI supervisor manifest JSON text. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="contract" /> is <see langword="null" />. </exception>
    public static string Serialize (GuiSupervisorManifestJsonContract contract)
    {
        if (contract == null)
        {
            throw new ArgumentNullException(nameof(contract));
        }

        var persistedContract = new PersistedContract(
            contract.SchemaVersion,
            contract.SessionToken.GetEncodedValue(),
            contract.ProjectFingerprint,
            ContractLiteralCodec.ToValue(contract.Endpoint.TransportKind),
            contract.Endpoint.Address,
            contract.ProcessId,
            contract.ProcessStartedAtUtc,
            contract.IssuedAtUtc);
        return JsonSerializer.Serialize(persistedContract, DaemonStorageJsonSerializerOptions.Serialize);
    }

    private sealed record PersistedContract (
        int SchemaVersion,
        string? SessionToken,
        ProjectFingerprint? ProjectFingerprint,
        string? EndpointTransportKind,
        string? EndpointAddress,
        int ProcessId,
        DateTimeOffset? ProcessStartedAtUtc,
        DateTimeOffset IssuedAtUtc)
    {
        public override string ToString () => nameof(PersistedContract);
    }
}
