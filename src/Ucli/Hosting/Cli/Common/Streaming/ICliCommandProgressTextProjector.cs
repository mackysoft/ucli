namespace MackySoft.Ucli.Hosting.Cli.Common.Streaming;

/// <summary> Projects command-specific progress payloads into human-readable text entries. </summary>
internal interface ICliCommandProgressTextProjector
{
    /// <summary> Tries to create one text entry for the specified progress event. </summary>
    /// <param name="eventName"> The command-specific event name. </param>
    /// <param name="payload"> The event payload. </param>
    /// <param name="text"> The unsanitized text entry when one should be emitted. </param>
    /// <returns> <see langword="true" /> when a text entry should be emitted; otherwise <see langword="false" />. </returns>
    bool TryCreateTextEntry (
        string eventName,
        object payload,
        out string text);
}
