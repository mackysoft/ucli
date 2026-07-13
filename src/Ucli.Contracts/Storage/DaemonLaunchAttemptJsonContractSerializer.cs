using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Provides shared serializer settings for daemon launch-attempt contracts. </summary>
internal static class DaemonLaunchAttemptJsonContractSerializer
{
    /// <summary> Deserializes daemon launch-attempt JSON text to contract. </summary>
    /// <param name="json"> The daemon launch-attempt JSON text. </param>
    /// <returns> The deserialized contract; or <see langword="null" /> when JSON root is <c>null</c>. </returns>
    public static DaemonLaunchAttemptJsonContract? Deserialize (string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON text must not be empty.", nameof(json));
        }

        return JsonSerializer.Deserialize<DaemonLaunchAttemptJsonContract>(
            json,
            DaemonStorageJsonSerializerOptions.Deserialize);
    }

    /// <summary> Serializes daemon launch-attempt contract to JSON text. </summary>
    /// <param name="contract"> The daemon launch-attempt contract. </param>
    /// <returns> The serialized daemon launch-attempt JSON text. </returns>
    public static string Serialize (DaemonLaunchAttemptJsonContract contract)
    {
        if (contract == null)
        {
            throw new ArgumentNullException(nameof(contract));
        }

        return JsonSerializer.Serialize(contract, DaemonStorageJsonSerializerOptions.Serialize);
    }
}
