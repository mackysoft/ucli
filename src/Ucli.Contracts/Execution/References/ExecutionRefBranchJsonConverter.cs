using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts;

/// <summary>Serializes a concrete execution-reference property through the shared lifecycle union.</summary>
internal abstract class ExecutionRefBranchJsonConverter<TExecutionRef> : JsonConverter<TExecutionRef>
    where TExecutionRef : ExecutionRef
{
    public sealed override TExecutionRef Read (
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var executionRef = JsonSerializer.Deserialize<ExecutionRef>(ref reader, options);
        return executionRef as TExecutionRef
            ?? throw new JsonException(
                $"Execution reference lifecycle does not match {typeof(TExecutionRef).Name}.");
    }

    public sealed override void Write (
        Utf8JsonWriter writer,
        TExecutionRef value,
        JsonSerializerOptions options) =>
        JsonSerializer.Serialize<ExecutionRef>(writer, value, options);
}

internal sealed class ActiveExecutionRefBranchJsonConverter :
    ExecutionRefBranchJsonConverter<ActiveExecutionRef>;

internal sealed class RecoveryExecutionRefBranchJsonConverter :
    ExecutionRefBranchJsonConverter<RecoveryExecutionRef>;

internal sealed class TerminalExecutionRefBranchJsonConverter :
    ExecutionRefBranchJsonConverter<TerminalExecutionRef>;
