using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Hosting.Reporting;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Emits shared Agent Skills command results using the uCLI JSON envelope. </summary>
internal sealed class UcliAgentSkillsCommandResultEmitter : IAgentSkillsCommandResultEmitter
{
    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="UcliAgentSkillsCommandResultEmitter" /> class. </summary>
    public UcliAgentSkillsCommandResultEmitter (ICommandResultWriter commandResultWriter)
    {
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <inheritdoc />
    public ValueTask<int> EmitAsync (
        AgentSkillsCommandResult result,
        AgentSkillsCommandOutputOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var commandResult = SkillsCommandResultFactory.Create(result);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return ValueTask.FromResult(commandResult.ExitCode);
    }
}
