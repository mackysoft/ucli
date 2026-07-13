namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;

/// <summary> Resolves process handling for endpoint-registration startup blockers. </summary>
internal static class DaemonStartupBlockedProcessPolicyResolver
{
    /// <summary> Resolves whether the blocked process should be terminated and how a kept process should be reported. </summary>
    /// <param name="policy"> The startup-blocked process policy requested by the caller. </param>
    /// <param name="editorMode"> The normalized daemon Editor mode. </param>
    /// <param name="ownerKind"> The normalized daemon session owner kind. </param>
    /// <param name="canShutdownProcess"> Whether uCLI owns enough process authority to terminate the process. </param>
    /// <param name="processId"> The observed Unity process identifier when available. </param>
    /// <returns> The resolved process policy. </returns>
    public static DaemonStartupBlockedProcessPolicyResolution Resolve (
        DaemonStartupBlockedProcessPolicy policy,
        DaemonEditorMode editorMode,
        DaemonSessionOwnerKind ownerKind,
        bool canShutdownProcess,
        int? processId)
    {
        if (processId is null)
        {
            return new DaemonStartupBlockedProcessPolicyResolution(
                ShouldTerminateProcess: false,
                ProcessActionWhenNotTerminated: DaemonStartupProcessAction.None);
        }

        if (!canShutdownProcess || ownerKind == DaemonSessionOwnerKind.User)
        {
            return Keep();
        }

        if (ownerKind != DaemonSessionOwnerKind.Cli)
        {
            return Keep();
        }

        if (editorMode == DaemonEditorMode.Batchmode)
        {
            return ResolveCliOwnedBatchmode(policy);
        }

        if (editorMode == DaemonEditorMode.Gui)
        {
            return ResolveCliOwnedGui(policy);
        }

        return Keep();
    }

    private static DaemonStartupBlockedProcessPolicyResolution ResolveCliOwnedBatchmode (
        DaemonStartupBlockedProcessPolicy policy)
    {
        return policy == DaemonStartupBlockedProcessPolicy.Keep ? Keep() : Terminate();
    }

    private static DaemonStartupBlockedProcessPolicyResolution ResolveCliOwnedGui (
        DaemonStartupBlockedProcessPolicy policy)
    {
        return policy == DaemonStartupBlockedProcessPolicy.Terminate ? Terminate() : Keep();
    }

    private static DaemonStartupBlockedProcessPolicyResolution Keep ()
    {
        return new DaemonStartupBlockedProcessPolicyResolution(
            ShouldTerminateProcess: false,
            ProcessActionWhenNotTerminated: DaemonStartupProcessAction.Kept);
    }

    private static DaemonStartupBlockedProcessPolicyResolution Terminate ()
    {
        return new DaemonStartupBlockedProcessPolicyResolution(
            ShouldTerminateProcess: true,
            ProcessActionWhenNotTerminated: DaemonStartupProcessAction.Unknown);
    }
}
