
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines protocol capabilities that are fixed by a Unity IPC method. </summary>
public static class UnityIpcMethodCapabilities
{
    /// <summary> Determines whether a method supports progress-frame streaming. </summary>
    /// <param name="method"> The defined Unity IPC method. </param>
    /// <returns> <see langword="true" /> for methods whose handlers support streaming; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="method" /> is undefined. </exception>
    public static bool SupportsStreaming (UnityIpcMethod method)
    {
        EnsureDefined(method);
        return method is UnityIpcMethod.BuildRun or UnityIpcMethod.TestRun;
    }

    /// <summary> Determines whether a method has server-side replay protection for the same request identifier. </summary>
    /// <param name="method"> The defined Unity IPC method. </param>
    /// <returns> <see langword="true" /> when response-loss recovery may resend the method with the same request identifier; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="method" /> is undefined. </exception>
    public static bool SupportsRecoverableReplay (UnityIpcMethod method)
    {
        EnsureDefined(method);
        return method is UnityIpcMethod.Compile or UnityIpcMethod.PlayEnter or UnityIpcMethod.PlayExit;
    }

    /// <summary> Determines whether replaying a request after an interrupted response is intrinsically side-effect free. </summary>
    /// <param name="method"> The defined Unity IPC method. </param>
    /// <returns> <see langword="true" /> when replay cannot repeat a mutation; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="method" /> is undefined. </exception>
    public static bool SupportsStatelessReadReplay (UnityIpcMethod method)
    {
        EnsureDefined(method);
        return method is UnityIpcMethod.Ping
            or UnityIpcMethod.OpsRead
            or UnityIpcMethod.IndexAssetsRead
            or UnityIpcMethod.IndexSceneTreeLiteRead
            or UnityIpcMethod.DaemonLogsRead
            or UnityIpcMethod.UnityLogsRead
            or UnityIpcMethod.PlayStatus;
    }

    /// <summary> Determines whether a method may be dispatched while the oneshot editor reports a non-ready startup state. </summary>
    /// <param name="method"> The defined Unity IPC method. </param>
    /// <param name="lifecycleState"> The defined editor lifecycle state reported by the startup probe. </param>
    /// <returns> <see langword="true" /> only when the method can produce useful diagnostics in the specified state; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when either argument is undefined. </exception>
    public static bool AllowsStartupLifecycleState (
        UnityIpcMethod method,
        IpcEditorLifecycleState lifecycleState)
    {
        EnsureDefined(method);
        if (!TextVocabulary.IsDefined(lifecycleState))
        {
            throw new ArgumentOutOfRangeException(nameof(lifecycleState), lifecycleState, "Editor lifecycle state must be defined.");
        }

        return method == UnityIpcMethod.Compile
            && lifecycleState is IpcEditorLifecycleState.CompileFailed or IpcEditorLifecycleState.SafeMode;
    }

    private static void EnsureDefined (UnityIpcMethod method)
    {
        if (!TextVocabulary.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method), method, "Unity IPC method must be defined.");
        }
    }
}
