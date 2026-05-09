using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal static class IpcPingResponseTestFactory
{
    public static IpcPingResponse Create (
        string serverVersion = "1.0.0",
        string editorMode = DaemonEditorModeValues.Batchmode,
        string unityVersion = "2023.2.22f1",
        string projectFingerprint = "project-fingerprint",
        string compileState = IpcCompileStateCodec.Ready,
        string? lifecycleState = IpcEditorLifecycleStateCodec.Ready,
        bool canAcceptExecutionRequests = true,
        string? blockingReason = null,
        string? compileGeneration = "0",
        string? domainReloadGeneration = "0")
    {
        return new IpcPingResponse(
            ServerVersion: serverVersion,
            EditorMode: editorMode,
            UnityVersion: unityVersion,
            ProjectFingerprint: projectFingerprint,
            CompileState: compileState,
            LifecycleState: lifecycleState,
            BlockingReason: blockingReason ?? ResolveBlockingReason(lifecycleState, canAcceptExecutionRequests),
            CompileGeneration: compileGeneration,
            DomainReloadGeneration: domainReloadGeneration,
            CanAcceptExecutionRequests: canAcceptExecutionRequests);
    }

    private static string? ResolveBlockingReason (
        string? lifecycleState,
        bool canAcceptExecutionRequests)
    {
        if (canAcceptExecutionRequests)
        {
            return null;
        }

        return lifecycleState switch
        {
            IpcEditorLifecycleStateCodec.Starting => IpcEditorBlockingReasonCodec.Startup,
            IpcEditorLifecycleStateCodec.Busy => IpcEditorBlockingReasonCodec.Busy,
            IpcEditorLifecycleStateCodec.Compiling => IpcEditorBlockingReasonCodec.Compile,
            IpcEditorLifecycleStateCodec.DomainReloading => IpcEditorBlockingReasonCodec.DomainReload,
            IpcEditorLifecycleStateCodec.Playmode => IpcEditorBlockingReasonCodec.PlayMode,
            IpcEditorLifecycleStateCodec.ModalBlocked => IpcEditorBlockingReasonCodec.ModalDialog,
            IpcEditorLifecycleStateCodec.SafeMode => IpcEditorBlockingReasonCodec.SafeMode,
            IpcEditorLifecycleStateCodec.ShuttingDown => IpcEditorBlockingReasonCodec.Shutdown,
            _ => null,
        };
    }
}
