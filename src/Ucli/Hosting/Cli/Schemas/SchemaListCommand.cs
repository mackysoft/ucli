using ConsoleAppFramework;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Schemas;

/// <summary> Lists the static schema set carried by the installed uCLI package. </summary>
internal sealed class SchemaListCommand
{
    private readonly IInstalledStaticSchemaSetProvider schemaSetProvider;
    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes the schema-list command. </summary>
    public SchemaListCommand (
        IInstalledStaticSchemaSetProvider schemaSetProvider,
        ICommandResultWriter commandResultWriter)
    {
        this.schemaSetProvider = schemaSetProvider ?? throw new ArgumentNullException(nameof(schemaSetProvider));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Lists the installed static schema manifest. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.ListSubcommand)]
    public int List (CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var schemaSet = SchemaCommandExecution.TryLoad(
            schemaSetProvider,
            commandResultWriter,
            UcliCommandNames.SchemaList);
        if (schemaSet == null)
        {
            return (int)CliExitCode.ToolError;
        }

        var result = CommandResult.Success(
            UcliCommandNames.SchemaList,
            "Installed static schemas listed.",
            schemaSet.Manifest);
        commandResultWriter.WriteToStandardOutput(result);
        return result.ExitCode;
    }
}
