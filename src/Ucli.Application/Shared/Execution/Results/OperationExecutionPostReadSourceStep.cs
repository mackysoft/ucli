using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.Results;

/// <summary> Represents one public step source fact used by post-read verification. </summary>
internal sealed record OperationExecutionPostReadSourceStep (
    IpcExecuteStepId OpId,
    IpcExecutePostReadSourceKind SourceKind,
    bool PlayModeMutation,
    IpcExecutePostReadCommit? Commit,
    bool PersistenceExpected,
    IpcExecuteExpectedPostState ExpectedPostState);
