using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

namespace MackySoft.Ucli.Tests;

internal static class UnityRequestExecutionResultTestFactory
{
    public static UnityRequestExecutionResult Failure (
        string message,
        UcliErrorCode errorCode)
    {
        return UnityRequestExecutionResult.Failure(new UnityRequestFailure(
            errorCode,
            message,
            RequestServiceResultPolicy.ResolveOutcome(errorCode)));
    }
}
