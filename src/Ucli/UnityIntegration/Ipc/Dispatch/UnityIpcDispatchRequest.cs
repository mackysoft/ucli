using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

/// <summary> Represents one IPC method dispatch request after application payload conversion. </summary>
internal sealed record UnityIpcDispatchRequest
{
    /// <summary> Initializes a new instance of the <see cref="UnityIpcDispatchRequest" /> class. </summary>
    /// <param name="method"> The defined Unity IPC method. </param>
    /// <param name="payload"> The IPC payload element. </param>
    /// <param name="launchOptions"> The explicit process launch options used only when oneshot execution is selected. </param>
    public UnityIpcDispatchRequest (
        UnityIpcMethod method,
        JsonElement payload,
        UnityBatchmodeLaunchOptions launchOptions)
    {
        if (!ContractLiteralCodec.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method), method, "Unity IPC method must be defined.");
        }

        ArgumentNullException.ThrowIfNull(launchOptions);
        if (launchOptions.ActiveBuildProfilePath is not null && method != UnityIpcMethod.BuildRun)
        {
            throw new ArgumentException(
                "An active Unity Build Profile may be specified only for build.run dispatch.",
                nameof(launchOptions));
        }

        Method = method;
        Payload = payload;
        LaunchOptions = launchOptions;
    }

    /// <summary> Gets the defined Unity IPC method. </summary>
    public UnityIpcMethod Method { get; }

    /// <summary> Gets the IPC payload element. </summary>
    public JsonElement Payload { get; }

    /// <summary> Gets whether daemon dispatch may replay this request with the same request id after endpoint recovery. </summary>
    public bool IsRecoverable => UnityIpcMethodCapabilities.SupportsRecoverableReplay(Method);

    /// <summary> Gets the process launch options used when oneshot execution is selected. </summary>
    public UnityBatchmodeLaunchOptions LaunchOptions { get; }
}
