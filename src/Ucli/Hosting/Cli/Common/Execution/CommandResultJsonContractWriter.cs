using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Common.Execution;

/// <summary> Writes command-result contracts with a fixed public JSON envelope. </summary>
internal sealed class CommandResultJsonContractWriter : JsonContractWriter<CommandResult>
{
    private static readonly JsonTypeInfo<CommandResult> CommandResultTypeInfo =
        (JsonTypeInfo<CommandResult>)CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(CommandResult));

    /// <inheritdoc />
    protected override void WriteCore (
        Utf8JsonWriter writer,
        CommandResult contract)
    {
        JsonSerializer.Serialize(writer, contract, CommandResultTypeInfo);
    }
}
