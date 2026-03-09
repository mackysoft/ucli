using System.Text.Json;
using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Execution.OperationExecute;

/// <summary> Describes one fixed operation execution triggered from one CLI command. </summary>
/// <param name="Command"> The top-level CLI command identifier used for timeout resolution and transport execution. </param>
/// <param name="OperationId"> The fixed operation identifier emitted in <c>ops[].id</c>. </param>
/// <param name="OperationName"> The fixed operation name emitted in <c>ops[].op</c>. </param>
/// <param name="Args"> The fixed operation argument payload emitted in <c>ops[].args</c>. </param>
internal sealed record OperationExecuteDefinition (
    UcliCommand Command,
    string OperationId,
    string OperationName,
    JsonElement Args);