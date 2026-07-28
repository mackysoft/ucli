using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Application.Features.Requests.Validate.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates command-level JSON results from <c>validate</c> service results. </summary>
internal static class ValidateCommandResultFactory
{
    /// <summary> Gets the serializer contract used by successful <c>validate</c> payloads. </summary>
    public static JsonTypeInfo SuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(ValidateExecutionOutput));

    /// <summary> Gets the serializer contract used by failed <c>validate</c> payloads. </summary>
    public static JsonTypeInfo ErrorPayloadTypeInfo { get; } =
        CommandErrorPayload.TypeInfo<ValidateErrorCommandPayload>();

    public static object CreateEmptyErrorPayload ()
    {
        return CommandErrorPayload.Empty<ValidateErrorCommandPayload>();
    }

    /// <summary> Creates one command result for <c>validate</c>. </summary>
    /// <param name="serviceResult"> The service result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult Create (ValidateServiceResult serviceResult)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);

        if (serviceResult.IsSuccess)
        {
            return CommandResult.Success(
                command: UcliCommandNames.Validate,
                message: serviceResult.Message,
                payload: serviceResult.Output!);
        }

        return CommandFailureProjector.Create(
            UcliCommandNames.Validate,
            serviceResult.Message,
            CommandErrorPayload.Detailed(new ValidateErrorCommandPayload(
                serviceResult.Output?.Project,
                serviceResult.Output?.ReadIndex)),
            serviceResult.Errors);
    }

    /// <summary> Creates one invalid-execution command result for <c>validate</c>. </summary>
    /// <param name="error"> The normalized execution error. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return CommandFailureProjector.Create(
            UcliCommandNames.Validate,
            ApplicationFailure.FromExecutionError(error),
            CommandErrorPayload.Detailed(new ValidateErrorCommandPayload()));
    }

    private sealed record ValidateErrorCommandPayload (
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        ProjectIdentityInfo? Project = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        ReadIndexInfo? ReadIndex = null)
        : CommandErrorPayload<ValidateErrorCommandPayload>;
}
