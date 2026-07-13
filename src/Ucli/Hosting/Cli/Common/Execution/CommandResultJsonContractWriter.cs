using System.Text.Json;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Serialization;

namespace MackySoft.Ucli.Hosting.Cli.Common.Execution;

/// <summary> Writes command-result contracts with a fixed public JSON envelope. </summary>
internal sealed class CommandResultJsonContractWriter : JsonContractWriter<CommandResult>
{
    /// <inheritdoc />
    protected override void WriteCore (
        Utf8JsonWriter writer,
        CommandResult contract)
    {
        writer.WriteStartObject();
        writer.WriteNumber("protocolVersion", contract.ProtocolVersion);
        writer.WriteString("command", contract.Command);
        writer.WriteString("status", contract.Status);
        writer.WriteNumber("exitCode", contract.ExitCode);
        writer.WriteString("message", contract.Message);
        writer.WritePropertyName("payload");
        WritePayload(writer, contract.Payload);
        WriteErrors(writer, contract.Errors);
        writer.WriteEndObject();
    }

    private static void WritePayload (
        Utf8JsonWriter writer,
        object? payload)
    {
        if (payload == null)
        {
            writer.WriteNullValue();
            return;
        }

        if (payload is JsonElement jsonElement)
        {
            jsonElement.WriteTo(writer);
            return;
        }

        JsonSerializer.Serialize(
            writer,
            payload,
            payload.GetType(),
            CliOutputJsonSerializerOptions.Default);
    }

    private static void WriteErrors (
        Utf8JsonWriter writer,
        IReadOnlyList<CommandError>? errors)
    {
        writer.WritePropertyName("errors");
        if (errors == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        for (var i = 0; i < errors.Count; i++)
        {
            var error = errors[i];
            writer.WriteStartObject();
            WriteNullableString(writer, "code", error.Code.Value);
            WriteNullableString(writer, "message", error.Message);
            WriteNullableString(writer, "opId", error.OpId);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}
