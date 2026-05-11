namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Startup;

/// <summary> Defines daemon startup-blocking reason values used before endpoint registration. </summary>
internal static class DaemonStartupBlockingReasonValues
{
    public const string SafeMode = "safeMode";

    public const string Compile = "compile";

    public const string PackageResolution = "packageResolution";

    public const string UcliPlugin = "ucliPlugin";

    public const string PrecompiledAssemblyConflict = "precompiledAssemblyConflict";

    public const string ModalDialog = "modalDialog";

    public const string EndpointNotRegistered = "endpointNotRegistered";

    public const string ProcessExit = "processExit";

    public const string Unknown = "unknown";
}
