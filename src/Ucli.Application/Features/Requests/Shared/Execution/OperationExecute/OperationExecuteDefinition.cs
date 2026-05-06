using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;

/// <summary> Describes one fixed operation execution triggered from one CLI command. </summary>
/// <param name="Command"> The top-level CLI command identifier used for timeout resolution and transport execution. </param>
/// <param name="OperationId"> The fixed operation identifier emitted in <c>steps[].id</c>. </param>
/// <param name="Descriptor"> The fixed operation metadata used for authorization and IPC emission. </param>
/// <param name="Args"> The fixed operation argument payload emitted in <c>steps[].args</c>. </param>
/// <param name="SuccessMessage"> The user-facing message emitted when this fixed operation succeeds. </param>
/// <param name="FailureMessage"> The fallback user-facing message emitted when this fixed operation fails without a more specific error message. </param>
internal sealed record OperationExecuteDefinition (
    UcliCommand Command,
    string OperationId,
    UcliOperationDescriptor Descriptor,
    JsonElement Args,
    string SuccessMessage,
    string FailureMessage);
