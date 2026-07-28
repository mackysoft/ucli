using ConsoleAppFramework;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Schemas;

/// <summary> Gets one static schema carried by the installed uCLI package. </summary>
internal sealed class SchemaGetCommand
{
    private readonly IInstalledStaticSchemaSetProvider schemaSetProvider;
    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes the schema-get command. </summary>
    public SchemaGetCommand (
        IInstalledStaticSchemaSetProvider schemaSetProvider,
        ICommandResultWriter commandResultWriter)
    {
        this.schemaSetProvider = schemaSetProvider ?? throw new ArgumentNullException(nameof(schemaSetProvider));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Gets one installed static schema by exact logical name. </summary>
    /// <param name="name"> Exact schema logical name shown by schema list. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.GetSubcommand)]
    public int Get (
        [Argument] string name,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();
        return Execute(name);
    }

    private int Execute (string name)
    {
        var inputError = SchemaGetCommandResultFactory.ValidateName(name);
        if (inputError is CommandResult error)
        {
            return SchemaCommandExecution.WriteError(
                commandResultWriter,
                error);
        }

        var schemaSet = SchemaCommandExecution.TryLoad(
            schemaSetProvider,
            commandResultWriter,
            UcliCommandNames.SchemaGet);
        if (schemaSet == null)
        {
            return (int)CliExitCode.ToolError;
        }

        var result = SchemaGetCommandResultFactory.Create(schemaSet, name);
        commandResultWriter.WriteToStandardOutput(result);
        return result.ExitCode;
    }
}
