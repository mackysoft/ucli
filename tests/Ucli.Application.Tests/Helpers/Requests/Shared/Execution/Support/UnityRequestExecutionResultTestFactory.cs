namespace MackySoft.Ucli.Application.Tests;

internal static class UnityRequestExecutionResultTestFactory
{
    public static UnityRequestExecutionResult Failure (
        string message,
        UcliCode errorCode)
    {
        return UnityRequestExecutionResult.Failure(new UnityRequestFailure(
            UnityRequestFailureKind.General,
            errorCode,
            message));
    }
}
