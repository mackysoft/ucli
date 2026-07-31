using System.Text.Json.Serialization;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Results;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Represents the public payload emitted by one fixed-operation request command. </summary>
internal sealed record OperationExecutionCommandPayload (
    Guid RequestId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ProjectIdentityInfo? Project,
    IReadOnlyList<OperationExecutionOperationResult> OpResults,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<OperationExecutionContractViolation>? ContractViolations,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ExecutionReadPostcondition? ReadPostcondition,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OperationExecutionPostReadSource? PostReadSource,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DaemonStartupObservationOutput? Startup,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DaemonDiagnosisOutput? Diagnosis,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DaemonStartupRetryDisposition? RetryDisposition,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? SafeToRetryImmediately)
    : CommandErrorPayload<OperationExecutionCommandPayload>;
