namespace MackySoft.Ucli.Hosting.Cli.Common.Contracts;

/// <summary> Represents a single error entry in the CLI JSON result payload. </summary>
/// <param name="Code"> A machine-readable error code. </param>
/// <param name="Message"> A human-readable error message for the current failure. </param>
/// <param name="OpId"> The operation identifier associated with this error, or <see langword="null" /> when not available. </param>
internal sealed record CommandError (
    string Code,
    string Message,
    string? OpId);