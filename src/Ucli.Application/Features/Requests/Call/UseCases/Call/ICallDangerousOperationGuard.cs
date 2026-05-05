using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;

/// <summary> Validates whether one prepared <c>call</c> request requires dangerous operations. </summary>
internal interface ICallDangerousOperationGuard
{
    /// <summary> Validates one prepared request and returns a blocking validation error when dangerous execution is forbidden. </summary>
    ValidationError? Validate (
        PhaseExecutionPreparedRequest preparedRequest,
        bool allowDangerous);
}
