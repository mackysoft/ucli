using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal static class IpcPingResponseTestFactory
{
    public static IpcPingResponse Create (
        string serverVersion = "1.0.0",
        string editorMode = "batchmode",
        string unityVersion = "2023.2.22f1",
        string projectFingerprint = "project-fingerprint",
        string compileState = "ready",
        string? lifecycleState = "ready",
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
        if (!ContractLiteralCodec.TryParse<IpcEditorLifecycleState>(lifecycleState, out var parsedLifecycleState)
            || IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(parsedLifecycleState) != canAcceptExecutionRequests)
        {
            return null;
        }

        var blockingReason = IpcEditorLifecycleSemantics.ResolveBlockingReason(parsedLifecycleState);
        return blockingReason.HasValue
            ? ContractLiteralCodec.ToValue(blockingReason.Value)
            : null;
    }
}
