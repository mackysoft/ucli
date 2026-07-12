using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Represents one parsed <c>execute</c> step contract. </summary>
/// <param name="Kind"> The parsed step kind. </param>
/// <param name="Id"> The parsed step identifier. </param>
/// <param name="OperationName"> The parsed operation name for <c>kind:"op"</c> steps. </param>
/// <param name="Element"> The cloned public step JSON object. </param>
internal sealed record IpcExecuteStepContract (
    IpcExecuteStepKind? Kind,
    string? Id,
    string? OperationName,
    JsonElement Element);
