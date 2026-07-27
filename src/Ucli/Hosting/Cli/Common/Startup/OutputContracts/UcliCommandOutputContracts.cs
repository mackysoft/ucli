using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Common.Startup.OutputContracts;

/// <summary> Creates validated registrations for the actual CLI payload serializer contracts. </summary>
internal static class UcliCommandOutputContracts
{
    internal static JsonTypeInfo EmptyPayloadTypeInfo { get; } =
        ResolveTypeInfo<EmptyCommandPayload>();

    internal static UcliCommandOutputContract Complete (
        string command,
        JsonTypeInfo successPayloadTypeInfo,
        JsonTypeInfo errorPayloadTypeInfo,
        Func<object> createDefaultErrorPayload)
    {
        ArgumentNullException.ThrowIfNull(successPayloadTypeInfo);
        return Create(
            command,
            successPayloadTypeInfo,
            errorPayloadTypeInfo,
            createDefaultErrorPayload);
    }

    internal static UcliCommandOutputContract ErrorOnly (string command)
    {
        return Create(
            command,
            successPayloadTypeInfo: null,
            EmptyPayloadTypeInfo,
            EmptyPayload);
    }

    internal static object EmptyPayload ()
    {
        return EmptyCommandPayload.Instance;
    }

    internal static JsonTypeInfo ResolveTypeInfo<T> ()
    {
        return CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(T));
    }

    private static UcliCommandOutputContract Create (
        string command,
        JsonTypeInfo? successPayloadTypeInfo,
        JsonTypeInfo errorPayloadTypeInfo,
        Func<object> createDefaultErrorPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(errorPayloadTypeInfo);
        ArgumentNullException.ThrowIfNull(createDefaultErrorPayload);
        EnsureObjectContract(command, CommandResultStatus.Ok, successPayloadTypeInfo);
        EnsureObjectContract(command, CommandResultStatus.Error, errorPayloadTypeInfo);

        return new UcliCommandOutputContract(
            command,
            successPayloadTypeInfo,
            errorPayloadTypeInfo,
            createDefaultErrorPayload);
    }

    private static void EnsureObjectContract (
        string command,
        CommandResultStatus status,
        JsonTypeInfo? typeInfo)
    {
        if (typeInfo != null && typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            throw new InvalidOperationException(
                $"CLI payload contract '{command}' ({status}) must use a JSON object serializer contract.");
        }
    }
}
