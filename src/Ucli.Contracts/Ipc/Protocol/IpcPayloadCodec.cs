using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Encodes and decodes IPC payload JSON elements using shared serializer options. </summary>
public static class IpcPayloadCodec
{
    /// <summary> Serializes one payload model to JSON element using shared IPC serializer options. </summary>
    /// <typeparam name="T"> The payload model type. </typeparam>
    /// <param name="value"> The payload model value. </param>
    /// <returns> The serialized JSON element. </returns>
    public static JsonElement SerializeToElement<T> (T value)
    {
        return JsonSerializer.SerializeToElement(value, IpcJsonSerializerOptions.Default);
    }

    /// <summary> Tries to deserialize one JSON element payload to target model type without throwing format exceptions. </summary>
    /// <typeparam name="T"> The target model type. </typeparam>
    /// <param name="element"> The source payload element. </param>
    /// <param name="value"> The deserialized payload value when operation succeeds. </param>
    /// <param name="error"> The machine-readable payload read error when operation fails. </param>
    /// <returns> <see langword="true" /> when payload is valid and deserialized; otherwise <see langword="false" />. </returns>
    public static bool TryDeserialize<T> (
        JsonElement element,
        out T value,
        out IpcPayloadReadError error)
    {
        try
        {
            var parsedValue = element.Deserialize<T>(IpcJsonSerializerOptions.Default);
            if (parsedValue is null)
            {
                value = default!;
                error = new IpcPayloadReadError(IpcPayloadReadErrorKind.NullPayload, "IPC payload is null.");
                return false;
            }

            value = parsedValue;
            error = IpcPayloadReadError.None;
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or NotSupportedException or ArgumentException)
        {
            value = default!;
            error = new IpcPayloadReadError(IpcPayloadReadErrorKind.DeserializeFailed, exception.Message);
            return false;
        }
    }
}
