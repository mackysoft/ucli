namespace MackySoft.Ucli.Application.Shared.Execution.Results;

/// <summary> Represents post-read source facts carried by one execute response. </summary>
internal sealed record OperationExecutionPostReadSource (
    int SchemaVersion,
    IReadOnlyList<OperationExecutionPostReadSourceStep> Steps);
