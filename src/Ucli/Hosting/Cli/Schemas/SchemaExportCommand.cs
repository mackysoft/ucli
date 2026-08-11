using ConsoleAppFramework;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Schemas;

/// <summary> Exports the static schema set carried by the installed uCLI package. </summary>
internal sealed class SchemaExportCommand
{
    private readonly IInstalledStaticSchemaSetProvider schemaSetProvider;
    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes the schema-export command. </summary>
    public SchemaExportCommand (
        IInstalledStaticSchemaSetProvider schemaSetProvider,
        ICommandResultWriter commandResultWriter)
    {
        this.schemaSetProvider = schemaSetProvider ?? throw new ArgumentNullException(nameof(schemaSetProvider));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Exports the exact installed static schema bytes to a new or empty directory. </summary>
    /// <param name="output">-o, Directory used as the exported schema-set root.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.ExportSubcommand)]
    public int Export (
        [AbsolutePathArgumentParser] AbsolutePath output,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();
        return Execute(output);
    }

    private int Execute (AbsolutePath output)
    {
        var schemaSet = SchemaCommandExecution.TryLoad(
            schemaSetProvider,
            commandResultWriter,
            UcliCommandNames.SchemaExport);
        if (schemaSet == null)
        {
            return (int)CliExitCode.ToolError;
        }

        var result = SchemaExportCommandExecution.Export(schemaSet, output);
        commandResultWriter.WriteToStandardOutput(result);
        return result.ExitCode;
    }
}
