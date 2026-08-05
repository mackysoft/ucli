using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary>Represents the runtime-owned slice of the recording capability observation.</summary>
public sealed record IpcGameViewRecordingCapabilityResponse
{
    [JsonConstructor]
    public IpcGameViewRecordingCapabilityResponse (
        GameViewRecordingAdapterCapability adapter,
        GameViewRecordingRuntimeAdmission runtimeAdmission,
        GameViewRecordingLimits? limits,
        GameViewRecordingCaptureProfile? captureProfile,
        IpcGameViewRecordingStartBinding? startBinding,
        GameViewRecordingRuntimeIdentity? observedRuntime = null)
    {
        Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        RuntimeAdmission = runtimeAdmission ?? throw new ArgumentNullException(nameof(runtimeAdmission));

        var registered = adapter.State == GameViewRecordingAdapterState.Registered;
        if (registered != (limits is not null && captureProfile is not null)
            || (!registered && (limits is not null || captureProfile is not null)))
        {
            throw new ArgumentException("Only a registered runtime adapter must publish limits and capture profile.", nameof(limits));
        }
        if (runtimeAdmission.State == GameViewRecordingRuntimeAdmissionState.Ready && !registered)
        {
            throw new ArgumentException("Runtime admission cannot be ready without a registered adapter.", nameof(runtimeAdmission));
        }
        var ready = runtimeAdmission.State == GameViewRecordingRuntimeAdmissionState.Ready;
        if (ready != (startBinding is not null))
        {
            throw new ArgumentException("Only ready runtime admission must carry a recording start binding.", nameof(startBinding));
        }
        if (ready && startBinding is not null && startBinding.Runtime != observedRuntime)
        {
            throw new ArgumentException(
                "A ready recording admission must report the runtime admitted by its start binding.",
                nameof(observedRuntime));
        }

        Limits = limits;
        CaptureProfile = captureProfile;
        StartBinding = startBinding;
        ObservedRuntime = observedRuntime;
    }

    public GameViewRecordingAdapterCapability Adapter { get; }

    public GameViewRecordingRuntimeAdmission RuntimeAdmission { get; }

    public GameViewRecordingLimits? Limits { get; }

    public GameViewRecordingCaptureProfile? CaptureProfile { get; }

    public IpcGameViewRecordingStartBinding? StartBinding { get; }

    /// <summary>Gets the Unity runtime observed for runtime-scoped recording selection, when observation succeeded.</summary>
    public GameViewRecordingRuntimeIdentity? ObservedRuntime { get; }
}
