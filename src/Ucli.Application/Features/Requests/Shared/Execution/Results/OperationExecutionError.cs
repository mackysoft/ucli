namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

/// <summary> Represents one application-level operation execution error. </summary>
/// <param name="Code"> The machine-readable error code. </param>
/// <param name="Message"> The human-readable error message. </param>
/// <param name="OpId"> The related operation identifier, or <see langword="null" /> when not applicable. </param>
internal sealed record OperationExecutionError (
    UcliCode Code,
    string Message,
    string? OpId);
