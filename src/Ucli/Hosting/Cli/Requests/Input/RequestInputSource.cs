namespace MackySoft.Ucli.Hosting.Cli.Requests.Input;

/// <summary> Identifies the source used to read one JSON request input. </summary>
internal enum RequestInputSource
{
    /// <summary> Indicates standard input was used. </summary>
    StandardInput = 0,

    /// <summary> Indicates a request file path was used. </summary>
    RequestPath = 1,
}
