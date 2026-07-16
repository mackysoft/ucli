using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Input;

/// <summary> Represents normalized source facts for one <c>verify --from</c> operation result. </summary>
internal sealed record VerifyFromPostReadSourceStep (
    IpcExecuteStepId OpId,
    IpcExecutePostReadSourceKind SourceKind,
    bool PlayModeMutation,
    IpcExecutePostReadCommit? Commit,
    bool PersistenceExpected,
    IpcExecuteExpectedPostState ExpectedPostState);
