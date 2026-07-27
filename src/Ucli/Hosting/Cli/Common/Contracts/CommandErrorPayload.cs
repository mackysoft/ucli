using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Hosting.Cli.Common.Contracts;

/// <summary>
/// Represents the closed runtime payload union for one command-specific error contract.
/// </summary>
internal abstract record CommandErrorPayload<TDetails>
    where TDetails : CommandErrorPayload<TDetails>;

/// <summary> Represents the common error branch with no command-specific details. </summary>
internal sealed record EmptyCommandErrorPayload<TDetails> : CommandErrorPayload<TDetails>
    where TDetails : CommandErrorPayload<TDetails>;

/// <summary> Creates values and serializer metadata for command-specific error unions. </summary>
internal static class CommandErrorPayload
{
    public static JsonTypeInfo TypeInfo<TDetails> ()
        where TDetails : CommandErrorPayload<TDetails>
    {
        return CliOutputJsonSerializerOptions.Default.GetTypeInfo(
            typeof(CommandErrorPayload<TDetails>));
    }

    public static IUcliNonNullJsonObject Empty<TDetails> ()
        where TDetails : CommandErrorPayload<TDetails>
    {
        return Wrap<TDetails>(new EmptyCommandErrorPayload<TDetails>());
    }

    public static IUcliNonNullJsonObject Detailed<TDetails> (TDetails details)
        where TDetails : CommandErrorPayload<TDetails>
    {
        ArgumentNullException.ThrowIfNull(details);
        return Wrap<TDetails>(details);
    }

    private static IUcliNonNullJsonObject Wrap<TDetails> (
        CommandErrorPayload<TDetails> payload)
        where TDetails : CommandErrorPayload<TDetails>
    {
        return UcliNonNullJsonObject.Wrap(
            payload,
            typeof(CommandErrorPayload<TDetails>),
            CliOutputJsonSerializerOptions.Default);
    }
}

/// <summary> Defines the serialized branch names for command-specific error payloads. </summary>
[VocabularyDefinition]
internal enum CommandErrorPayloadKind
{
    [VocabularyText("empty")]
    Empty = 0,

    [VocabularyText("detailed")]
    Detailed = 1,
}
