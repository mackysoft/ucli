using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Writes <c>ops/&lt;operationStorageKey&gt;.json</c> contracts with a fixed public JSON shape. </summary>
internal sealed class IndexOpsDescribeJsonContractWriter : IndexJsonContractWriterBase<IndexOpsDescribeJsonContract>
{
    /// <inheritdoc />
    protected override void WriteCore (
        Utf8JsonWriter writer,
        IndexOpsDescribeJsonContract contract)
    {
        writer.WriteStartObject();
        WriteRootHeader(writer, contract.SchemaVersion, contract.GeneratedAtUtc);
        WriteNullableString(writer, "sourceInputsHash", contract.SourceInputsHash);
        writer.WritePropertyName("operation");
        if (contract.Operation == null)
        {
            writer.WriteNullValue();
        }
        else
        {
            IndexOpEntryJsonContractWriter.WriteEntry(writer, contract.Operation);
        }

        writer.WriteEndObject();
    }
}
