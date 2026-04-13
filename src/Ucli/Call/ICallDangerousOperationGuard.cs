using MackySoft.Ucli.Contracts.Ipc.Validation;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Operations;

namespace MackySoft.Ucli.Call;

/// <summary> Validates whether one prepared <c>call</c> request requires dangerous operations. </summary>
internal interface ICallDangerousOperationGuard
{
    /// <summary> Validates one prepared request and returns a blocking validation error when dangerous execution is forbidden. </summary>
    ValidationError? Validate (
        PhaseExecutionPreparedRequest preparedRequest,
        bool allowDangerous);
}