namespace MackySoft.Ucli.Hosting.Cli.Common.Streaming;

/// <summary> Defines the public stream-entry rendering format selected by CLI options. </summary>
internal enum CliStreamEntryFormat
{
    /// <summary> Renders entries as human-readable text lines. </summary>
    Text = 0,

    /// <summary> Renders entries as NDJSON entry envelopes. </summary>
    Json = 1,
}
