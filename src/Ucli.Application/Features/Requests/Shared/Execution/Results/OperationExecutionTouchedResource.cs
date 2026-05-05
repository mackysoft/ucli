namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

/// <summary> Represents one resource touched by operation execution. </summary>
/// <param name="Kind"> The touched resource kind. </param>
/// <param name="Path"> The project-relative resource path. </param>
/// <param name="Guid"> The optional asset guid when available. </param>
internal sealed record OperationExecutionTouchedResource (
    string Kind,
    string Path,
    string? Guid);
