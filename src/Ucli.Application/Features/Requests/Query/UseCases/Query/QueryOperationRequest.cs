using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;

/// <summary> Represents one typed query operation request. </summary>
internal abstract record QueryOperationRequest (
    string CommandName,
    IpcExecuteStepId OperationId,
    string OperationName);
