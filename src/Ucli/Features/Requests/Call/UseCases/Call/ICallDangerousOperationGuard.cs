using MackySoft.Ucli.Contracts.Ipc.Validation;
using MackySoft.Ucli.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

namespace MackySoft.Ucli.Features.Requests.Call.UseCases.Call;

/// <summary> Validates whether one prepared <c>call</c> request requires dangerous operations. </summary>
internal interface ICallDangerousOperationGuard
{
    /// <summary> Validates one prepared request and returns a blocking validation error when dangerous execution is forbidden. </summary>
    ValidationError? Validate (
        PhaseExecutionPreparedRequest preparedRequest,
        bool allowDangerous);
}