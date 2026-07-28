using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Represents one step element in a normalized validation request. </summary>
/// <param name="Kind"> The parsed step kind. </param>
/// <param name="StepIndex"> The zero-based position of the step in the request. </param>
/// <param name="Op"> The operation name for <c>kind:"op"</c> steps. </param>
/// <param name="Args"> The operation arguments for <c>kind:"op"</c> steps. </param>
internal sealed record ValidateRequestStep (
    IpcExecuteStepKind Kind,
    int StepIndex,
    string? Op,
    JsonElement Args)
{
    /// <summary>Gets the validated edit execution model for an edit step.</summary>
    public IpcEditStepContract? EditContract { get; init; }
}
