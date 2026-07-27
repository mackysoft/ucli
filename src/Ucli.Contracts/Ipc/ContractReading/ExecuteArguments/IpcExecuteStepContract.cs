using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Represents one parsed <c>execute</c> step contract. </summary>
/// <param name="Kind"> The parsed step kind. </param>
/// <param name="Id"> The parsed step identifier. </param>
/// <param name="OperationName"> The parsed operation name for <c>kind:"op"</c> steps. </param>
/// <param name="Element"> The cloned public step JSON object. </param>
internal sealed record IpcExecuteStepContract (
    IpcExecuteStepKind Kind,
    IpcExecuteStepId Id,
    string? OperationName,
    JsonElement Element)
{
    /// <summary>Gets the operation arguments from the authoritative deserialized request DTO.</summary>
    public JsonElement OperationArgs { get; init; }

    /// <summary>Gets the mapped edit execution model for an edit step.</summary>
    public IpcEditStepContract? EditContract { get; init; }
}
