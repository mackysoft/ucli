using MackySoft.Ucli.Application.Shared.Foundation;
namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;

/// <summary> Represents one Unity Editor Console clear IPC attempt result. </summary>
/// <param name="Error"> The structured error when clear fails. </param>
internal sealed record UnityConsoleClearClientResult (
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the clear attempt succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful client result. </summary>
    /// <returns> The successful client result. </returns>
    public static UnityConsoleClearClientResult Success ()
    {
        return new UnityConsoleClearClientResult(Error: null);
    }

    /// <summary> Creates a failed client result. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The failed client result. </returns>
    public static UnityConsoleClearClientResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UnityConsoleClearClientResult(error);
    }
}
