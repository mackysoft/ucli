using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

namespace MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;

/// <summary> Validates whether one prepared <c>call</c> request requires dangerous operations. </summary>
internal interface ICallDangerousOperationGuard
{
    /// <summary> Validates one prepared request and returns a blocking validation error when dangerous execution is forbidden. </summary>
    ValidationError? Validate (
        PhaseExecutionPreparedRequest preparedRequest,
        bool allowDangerous);
}
