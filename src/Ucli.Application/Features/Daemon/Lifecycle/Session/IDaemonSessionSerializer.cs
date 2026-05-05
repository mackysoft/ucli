namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Converts daemon session models to and from JSON text representations. </summary>
internal interface IDaemonSessionSerializer
{
    /// <summary> Deserializes daemon session JSON text to model. </summary>
    /// <param name="json"> The daemon session JSON text. </param>
    /// <returns> The deserialized daemon session model. </returns>
    DaemonSession Deserialize (string json);

    /// <summary> Serializes daemon session model to JSON text. </summary>
    /// <param name="session"> The daemon session model. </param>
    /// <returns> The serialized daemon session JSON text. </returns>
    string Serialize (DaemonSession session);
}
