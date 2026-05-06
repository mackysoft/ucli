using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Common.Execution;

/// <summary> Serializes command results and writes them to standard output as JSON. </summary>
internal sealed class CommandResultWriter : ICommandResultWriter
{
    private readonly IJsonContractWriter<CommandResult> jsonContractWriter;

    /// <summary> Initializes a new instance of the <see cref="CommandResultWriter" /> class. </summary>
    /// <param name="jsonContractWriter"> The command-result JSON contract writer. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="jsonContractWriter" /> is <see langword="null" />. </exception>
    public CommandResultWriter (IJsonContractWriter<CommandResult> jsonContractWriter)
    {
        this.jsonContractWriter = jsonContractWriter ?? throw new ArgumentNullException(nameof(jsonContractWriter));
    }

    /// <summary> Writes the specified command result to standard output in JSON format. </summary>
    /// <param name="result"> The command result to serialize. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="result" /> is <see langword="null" />. </exception>
    public void WriteToStandardOutput (CommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Console.Out.Write(jsonContractWriter.Write(result));
    }

}
