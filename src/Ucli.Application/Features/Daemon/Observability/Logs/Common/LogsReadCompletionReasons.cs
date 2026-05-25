namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;

/// <summary> Defines public completion-reason literals for <c>logs * read</c>. </summary>
internal static class LogsReadCompletionReasons
{
    public const string Completed = "completed";
    public const string IdleTimeout = "idleTimeout";
    public const string UntilReached = "untilReached";
    public const string Canceled = "canceled";
    public const string Error = "error";
}
