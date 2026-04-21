using System.Collections.Generic;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines IPC command names used by request contracts and execution dispatch. </summary>
public static class IpcExecuteCommandNames
{
    /// <summary> Gets the execute-command identifiers recognized by protocol contracts. </summary>
    public static IReadOnlyCollection<UcliCommand> KnownCommands { get; } =
    [
        UcliCommandIds.Validate,
        UcliCommandIds.Plan,
        UcliCommandIds.Call,
        UcliCommandIds.Resolve,
        UcliCommandIds.Query,
    ];

    /// <summary> Gets the execute-command identifiers that require operation-pipeline execution. </summary>
    public static IReadOnlyCollection<UcliCommand> OperationPipelineCommands { get; } =
    [
        UcliCommandIds.Plan,
        UcliCommandIds.Call,
        UcliCommandIds.Resolve,
        UcliCommandIds.Query,
    ];

    private static readonly HashSet<UcliCommand> KnownCommandSet = new(KnownCommands);

    private static readonly HashSet<UcliCommand> OperationPipelineCommandSet = new(OperationPipelineCommands);

    /// <summary> Determines whether the command name is recognized by protocol contracts. </summary>
    /// <param name="commandName"> The command name to check. </param>
    /// <returns> <see langword="true" /> when recognized; otherwise <see langword="false" />. </returns>
    public static bool IsKnown (string? commandName)
    {
        if (!UcliCommand.TryCreate(commandName, out var command))
        {
            return false;
        }

        return KnownCommandSet.Contains(command);
    }

    /// <summary> Determines whether the command requires operation-pipeline execution. </summary>
    /// <param name="commandName"> The command name to check. </param>
    /// <returns> <see langword="true" /> when command is handled by operation-pipeline execution; otherwise <see langword="false" />. </returns>
    public static bool IsOperationPipelineCommand (string? commandName)
    {
        if (!UcliCommand.TryCreate(commandName, out var command))
        {
            return false;
        }

        return OperationPipelineCommandSet.Contains(command);
    }
}