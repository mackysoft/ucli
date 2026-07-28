using System.Text.Json;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Schemas;

/// <summary> Provides failure handling shared by the static-schema leaf commands. </summary>
internal static class SchemaCommandExecution
{
    /// <summary> Loads and validates the installed schema set, emitting a command error when it is unavailable. </summary>
    public static UcliStaticSchemaSet? TryLoad (
        IInstalledStaticSchemaSetProvider schemaSetProvider,
        ICommandResultWriter commandResultWriter,
        string command)
    {
        ArgumentNullException.ThrowIfNull(schemaSetProvider);
        ArgumentNullException.ThrowIfNull(commandResultWriter);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        try
        {
            return schemaSetProvider.Load();
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or InvalidDataException
                                          or JsonException)
        {
            WriteError(
                commandResultWriter,
                CommandResult.InternalError(
                    command,
                    $"Installed static schema set is invalid. {exception.Message}"));
            return null;
        }
    }

    /// <summary> Writes one command error and returns its exit code. </summary>
    public static int WriteError (
        ICommandResultWriter commandResultWriter,
        CommandResult result)
    {
        ArgumentNullException.ThrowIfNull(commandResultWriter);

        commandResultWriter.WriteToStandardOutput(result);
        return result.ExitCode;
    }
}
