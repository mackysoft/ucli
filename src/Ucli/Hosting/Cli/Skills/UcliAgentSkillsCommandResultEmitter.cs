using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Hosting.Reporting;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Emits shared Agent Skills command results using the uCLI JSON envelope. </summary>
internal sealed class UcliAgentSkillsCommandResultEmitter : IAgentSkillsCommandResultEmitter
{
    private readonly ICommandResultWriter commandResultWriter;
    private readonly SkillHostAdapterSet hostAdapters;

    /// <summary> Initializes a new instance of the <see cref="UcliAgentSkillsCommandResultEmitter" /> class. </summary>
    public UcliAgentSkillsCommandResultEmitter (
        ICommandResultWriter commandResultWriter,
        SkillHostAdapterSet hostAdapters)
    {
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
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

        var commandResult = SkillsCommandResultFactory.Create(result, hostAdapters);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return ValueTask.FromResult(commandResult.ExitCode);
    }
}
