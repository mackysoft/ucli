using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Represents the result of query window option normalization. </summary>
internal sealed record QueryWindowOptionsCreationResult (
    BoundedWindowOptions? Options,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether normalization succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates one successful normalization result. </summary>
    public static QueryWindowOptionsCreationResult Success (BoundedWindowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new QueryWindowOptionsCreationResult(options, null);
    }

    /// <summary> Creates one failed normalization result. </summary>
    public static QueryWindowOptionsCreationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new QueryWindowOptionsCreationResult(null, error);
    }
}
