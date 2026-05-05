using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Represents one step element in a normalized validation request. </summary>
/// <param name="Kind"> The parsed step kind. </param>
/// <param name="StepId"> The step identifier. </param>
/// <param name="Op"> The operation name for <c>kind:"op"</c> steps. </param>
/// <param name="Element"> The cloned public step JSON object. </param>
internal sealed record ValidateRequestStep (
    IpcRequestStepKind? Kind,
    string? StepId,
    string? Op,
    JsonElement Element);
