using System.Text.Json;

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

        return JsonSerializer.Deserialize<GuiSupervisorManifestJsonContract>(
            json,
            DaemonStorageJsonSerializerOptions.Deserialize);
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

        return JsonSerializer.Serialize(contract, DaemonStorageJsonSerializerOptions.Serialize);
    }
}
