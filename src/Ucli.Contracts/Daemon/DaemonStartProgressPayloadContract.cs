namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines the payload-kind contract for daemon-start progress events. </summary>
public static class DaemonStartProgressPayloadContract
{
    /// <summary> Resolves the payload kind required by one daemon-start progress event. </summary>
    public static bool TryGetPayloadKind (
        DaemonStartProgressEvent progressEvent,
        out DaemonStartProgressPayloadKind payloadKind)
    {
        switch (progressEvent)
        {
            case DaemonStartProgressEvent.Launching:
            case DaemonStartProgressEvent.WaitingForEndpoint:
            case DaemonStartProgressEvent.BlockerDetected:
            case DaemonStartProgressEvent.SessionRegistered:
            case DaemonStartProgressEvent.EndpointRegistered:
                payloadKind = DaemonStartProgressPayloadKind.StartupObservation;
                return true;
            case DaemonStartProgressEvent.LifecycleObserved:
                payloadKind = DaemonStartProgressPayloadKind.LifecycleSnapshot;
                return true;
            default:
                payloadKind = default;
                return false;
        }
    }

    /// <summary> Returns whether the event carries a startup-observation progress payload. </summary>
    public static bool IsStartupObservation (DaemonStartProgressEvent progressEvent)
    {
        return TryGetPayloadKind(progressEvent, out var payloadKind)
            && payloadKind == DaemonStartProgressPayloadKind.StartupObservation;
    }

    /// <summary> Returns whether the event carries a lifecycle-snapshot progress payload. </summary>
    public static bool IsLifecycleSnapshot (DaemonStartProgressEvent progressEvent)
    {
        return TryGetPayloadKind(progressEvent, out var payloadKind)
            && payloadKind == DaemonStartProgressPayloadKind.LifecycleSnapshot;
    }
}
