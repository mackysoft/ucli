using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;

/// <summary> Describes one fixed operation execution triggered from one CLI command. </summary>
internal sealed record OperationExecuteDefinition
{
    /// <summary> Initializes one fixed-operation execution definition. </summary>
    /// <param name="command"> The top-level CLI command identifier used for timeout resolution and transport execution. </param>
    /// <param name="operationId"> The fixed operation identifier emitted in <c>steps[].id</c>. </param>
    /// <param name="operationName"> The registered operation name resolved from the authoritative project catalog. </param>
    /// <param name="args"> The fixed operation argument payload emitted in <c>steps[].args</c>. </param>
    /// <param name="successMessage"> The user-facing message emitted when this fixed operation succeeds. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="operationName" /> is <see langword="null" />, empty, or whitespace. </exception>
    public OperationExecuteDefinition (
        UcliCommand command,
        IpcExecuteStepId operationId,
        string operationName,
        JsonElement args,
        string successMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(successMessage);

        Command = command;
        OperationId = operationId;
        OperationName = operationName;
        Args = args;
        SuccessMessage = successMessage;
    }

    /// <summary> Gets the top-level CLI command identifier used for timeout resolution and transport execution. </summary>
    public UcliCommand Command { get; }

    /// <summary> Gets the fixed operation identifier emitted in <c>steps[].id</c>. </summary>
    public IpcExecuteStepId OperationId { get; }

    /// <summary> Gets the registered operation name resolved from the authoritative project catalog. </summary>
    public string OperationName { get; }

    /// <summary> Gets the fixed operation argument payload emitted in <c>steps[].args</c>. </summary>
    public JsonElement Args { get; }

    /// <summary> Gets the user-facing message emitted when this fixed operation succeeds. </summary>
    public string SuccessMessage { get; }
}
