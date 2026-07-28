using System.Text.Json.Serialization;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Application.Shared.Execution.Results;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Represents the public payload shared by request commands that report read-index selection. </summary>
internal sealed record ReadIndexRequestCommandPayload (
    Guid RequestId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ProjectIdentityInfo? Project,
    IReadOnlyList<OperationExecutionOperationResult> OpResults,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<OperationExecutionContractViolation>? ContractViolations,
    ReadIndexInfo ReadIndex,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DaemonStartupObservationOutput? Startup,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DaemonDiagnosisOutput? Diagnosis,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DaemonStartupRetryDisposition? RetryDisposition,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? SafeToRetryImmediately)
    : CommandErrorPayload<ReadIndexRequestCommandPayload>;
