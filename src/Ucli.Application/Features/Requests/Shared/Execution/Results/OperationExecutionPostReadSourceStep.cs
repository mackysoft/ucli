namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

/// <summary> Represents one public step source fact used by post-read verification. </summary>
internal sealed record OperationExecutionPostReadSourceStep (
    string OpId,
    string SourceKind,
    bool PlayModeMutation,
    string? Commit,
    bool PersistenceExpected,
    string ExpectedPostState);
