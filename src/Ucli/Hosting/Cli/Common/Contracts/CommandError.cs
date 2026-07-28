namespace MackySoft.Ucli.Hosting.Cli.Common.Contracts;

/// <summary> Represents a single error entry in the CLI JSON result payload. </summary>
/// <param name="Code"> A machine-readable error code. </param>
/// <param name="Message"> A human-readable error message for the current failure. </param>
/// <param name="InstancePath"> The RFC 6901 path of the related value, or <see langword="null" /> when not available. </param>
internal sealed record CommandError
{
    public CommandError (
        UcliCode Code,
        string Message,
        string? InstancePath)
    {
        this.Code = Code ?? throw new ArgumentNullException(nameof(Code));
        this.Message = Message;
        this.InstancePath = InstancePath;
    }

    public UcliCode Code { get; }

    public string Message { get; }

    public string? InstancePath { get; }
}
