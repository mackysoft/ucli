using System.Text.Json;
using System.Text.Json.Serialization;

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Serializes scene-tree source-state kinds as public contract literals. </summary>
public sealed class SceneTreeSourceStateKindJsonConverter : JsonConverter<SceneTreeSourceStateKind>
{
    /// <inheritdoc />
    public override SceneTreeSourceStateKind Read (
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Scene-tree source-state kind must be a string.");
        }

        var value = reader.GetString();
        if (!ContractLiteralCodec.TryParse<SceneTreeSourceStateKind>(value, out var sourceStateKind))
        {
            throw new JsonException($"Unsupported scene-tree source-state kind '{value}'.");
        }

        return sourceStateKind;
    }

    /// <inheritdoc />
    public override void Write (
        Utf8JsonWriter writer,
        SceneTreeSourceStateKind value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(ContractLiteralCodec.ToValue(value));
    }
}
