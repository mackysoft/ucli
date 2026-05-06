using System.Text.Json;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Provides shared write helpers for index JSON contracts. </summary>
/// <typeparam name="TContract"> The read-index contract type. </typeparam>
internal abstract class IndexJsonContractWriterBase<TContract> : JsonContractWriter<TContract>
{
    protected static void WriteArray<TItem> (
        Utf8JsonWriter writer,
        string propertyName,
        IReadOnlyList<TItem>? items,
        Action<Utf8JsonWriter, TItem> writeItem)
    {
        writer.WritePropertyName(propertyName);
        if (items == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        for (var i = 0; i < items.Count; i++)
        {
            writeItem(writer, items[i]);
        }

        writer.WriteEndArray();
    }
}
