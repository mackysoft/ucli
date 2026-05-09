namespace MackySoft.Ucli.Contracts;

internal static class UcliErrorInspectTargets
{
    public const string DaemonStatusCommand = "ucli daemon status";

    public const string DaemonListCommand = "ucli daemon list";

    public const string DaemonErrorLogsCommand = "ucli logs daemon read --level error";

    public const string UnityErrorLogsCommand = "ucli logs unity read --level error";
}
