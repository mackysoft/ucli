using System.Text.Json.Serialization.Metadata;

namespace MackySoft.Ucli.Hosting.Cli.Common.Startup.OutputContracts;

/// <summary>
/// Describes the effective serializer contracts emitted for one public command result name.
/// </summary>
internal sealed record UcliCommandOutputContract (
    string Command,
    JsonTypeInfo? SuccessPayloadTypeInfo,
    JsonTypeInfo ErrorPayloadTypeInfo,
    Func<object> CreateDefaultErrorPayload);
