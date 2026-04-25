namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the result payload returned by one <c>ucli.resolve</c> operation result. </summary>
/// <param name="GlobalObjectId"> The resolved GlobalObjectId string. </param>
public sealed record IpcResolveOperationResult (
    string GlobalObjectId);