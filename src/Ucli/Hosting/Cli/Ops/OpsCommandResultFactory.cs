using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Ops;

/// <summary> Creates command-level JSON results from <c>ops</c> service results. </summary>
internal static class OpsCommandResultFactory
{
    /// <summary> Gets the serializer contract used by successful <c>ops list</c> payloads. </summary>
    public static JsonTypeInfo ListSuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(OpsListExecutionOutput));

    /// <summary> Gets the serializer contract used by successful <c>ops describe</c> payloads. </summary>
    public static JsonTypeInfo DescribeSuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(OpsDescribeExecutionOutput));

    /// <summary> Gets the serializer contract used by failed <c>ops</c> payloads. </summary>
    public static JsonTypeInfo ErrorPayloadTypeInfo { get; } =
        CommandErrorPayload.TypeInfo<OpsFailureCommandPayload>();

    /// <summary> Creates the common error branch with no operation-catalog details. </summary>
    public static object CreateEmptyErrorPayload ()
    {
        return CommandErrorPayload.Empty<OpsFailureCommandPayload>();
    }

    public static CommandResult CreateExecutionError (
        string command,
        ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return CommandFailureProjector.Create(
            command,
            ApplicationFailure.FromExecutionError(error),
            CreateEmptyErrorPayload());
    }

    /// <summary> Creates one command result for <c>ops list</c>. </summary>
    /// <param name="serviceResult"> The service result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateList (OpsListServiceResult serviceResult)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);

        if (serviceResult.IsSuccess)
        {
            return CommandResult.Success(
                command: UcliCommandNames.OpsList,
                message: serviceResult.Message,
                payload: serviceResult.Output!);
        }

        return CreateFailure(
            UcliCommandNames.OpsList,
            serviceResult.Message,
            serviceResult.ErrorCode,
            serviceResult.StartupFailure,
            CommandErrorPayload.Detailed(new OpsFailureCommandPayload(
                serviceResult.StartupFailure?.Startup,
                serviceResult.StartupFailure?.Diagnosis,
                serviceResult.StartupFailure?.RetryDisposition,
                serviceResult.StartupFailure?.SafeToRetryImmediately)));
    }

    /// <summary> Creates one command result for <c>ops describe</c>. </summary>
    /// <param name="serviceResult"> The service result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateDescribe (OpsDescribeServiceResult serviceResult)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);

        if (serviceResult.IsSuccess)
        {
            return CommandResult.Success(
                command: UcliCommandNames.OpsDescribe,
                message: serviceResult.Message,
                payload: serviceResult.Output!);
        }

        return CreateFailure(
            UcliCommandNames.OpsDescribe,
            serviceResult.Message,
            serviceResult.ErrorCode,
            serviceResult.StartupFailure,
            CommandErrorPayload.Detailed(new OpsFailureCommandPayload(
                serviceResult.StartupFailure?.Startup,
                serviceResult.StartupFailure?.Diagnosis,
                serviceResult.StartupFailure?.RetryDisposition,
                serviceResult.StartupFailure?.SafeToRetryImmediately)));
    }

    private static CommandResult CreateFailure (
        string command,
        string message,
        UcliCode? errorCode,
        StartupFailureDetail? startupFailure,
        object payload)
    {
        return CommandFailureProjector.Create(
            command,
            ApplicationFailure.FromCode(errorCode, message, startupFailure: startupFailure),
            payload);
    }

    private sealed record OpsFailureCommandPayload (
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonStartupObservationOutput? Startup,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonDiagnosisOutput? Diagnosis,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonStartupRetryDisposition? RetryDisposition,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        bool? SafeToRetryImmediately)
        : CommandErrorPayload<OpsFailureCommandPayload>;
}
