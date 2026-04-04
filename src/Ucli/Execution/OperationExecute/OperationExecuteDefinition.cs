using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Operations;

namespace MackySoft.Ucli.Execution.OperationExecute;

/// <summary> Describes one fixed operation execution triggered from one CLI command. </summary>
/// <param name="Command"> The top-level CLI command identifier used for timeout resolution and transport execution. </param>
/// <param name="OperationId"> The fixed operation identifier emitted in <c>steps[].id</c>. </param>
/// <param name="Descriptor"> The fixed operation metadata used for authorization and IPC emission. </param>
/// <param name="Args"> The fixed operation argument payload emitted in <c>steps[].args</c>. </param>
internal sealed record OperationExecuteDefinition (
    UcliCommand Command,
    string OperationId,
    UcliOperationDescriptor Descriptor,
    JsonElement Args);