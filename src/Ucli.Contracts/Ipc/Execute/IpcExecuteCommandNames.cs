using System;
using System.Collections.Generic;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines IPC command names used by request contracts and execution dispatch. </summary>
public static class IpcExecuteCommandNames
{
    /// <summary> Gets the command name used for static request validation. </summary>
    public const string Validate = "validate";

    /// <summary> Gets the command name used for planning execution. </summary>
    public const string Plan = "plan";

    /// <summary> Gets the command name used for call execution. </summary>
    public const string Call = "call";

    /// <summary> Gets the command name used for object resolution. </summary>
    public const string Resolve = "resolve";

    /// <summary> Gets the command name used for read-only query execution. </summary>
    public const string Query = "query";

    /// <summary> Gets the command name used for refresh execution. </summary>
    public const string Refresh = "refresh";

    private static readonly HashSet<string> KnownCommandNames = new(StringComparer.Ordinal)
    {
        Validate,
        Plan,
        Call,
        Resolve,
        Query,
        Refresh,
    };

    private static readonly HashSet<string> OperationPipelineCommandNames = new(StringComparer.Ordinal)
    {
        Plan,
        Call,
        Resolve,
        Query,
        Refresh,
    };

    /// <summary> Determines whether the command name is recognized by protocol contracts. </summary>
    /// <param name="commandName"> The command name to check. </param>
    /// <returns> <see langword="true" /> when recognized; otherwise <see langword="false" />. </returns>
    public static bool IsKnown (string? commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            return false;
        }

        return KnownCommandNames.Contains(commandName);
    }

    /// <summary> Determines whether the command requires operation-pipeline execution. </summary>
    /// <param name="commandName"> The command name to check. </param>
    /// <returns> <see langword="true" /> when command is handled by operation-pipeline execution; otherwise <see langword="false" />. </returns>
    public static bool IsOperationPipelineCommand (string? commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            return false;
        }

        return OperationPipelineCommandNames.Contains(commandName);
    }
}