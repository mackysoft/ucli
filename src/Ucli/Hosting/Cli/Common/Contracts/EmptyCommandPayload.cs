namespace MackySoft.Ucli.Hosting.Cli.Common.Contracts;

/// <summary> Represents the closed empty payload emitted by command failures with no command-specific detail. </summary>
internal sealed record EmptyCommandPayload
{
    /// <summary> Gets the shared empty command payload. </summary>
    public static EmptyCommandPayload Instance { get; } = new();

    private EmptyCommandPayload ()
    {
    }
}
