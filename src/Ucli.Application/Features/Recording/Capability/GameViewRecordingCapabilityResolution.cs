using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Application.Features.Recording.Capability;

/// <summary>Represents either an admitted recording start or its structured rejection.</summary>
internal abstract record GameViewRecordingCapabilityResolution
{
    protected GameViewRecordingCapabilityResolution (
        GameViewRecordingCapability capability,
        GameViewRecordingRuntimeIdentity? observedRuntime)
    {
        Capability = capability ?? throw new ArgumentNullException(nameof(capability));
        ObservedRuntime = observedRuntime;
    }

    public GameViewRecordingCapability Capability { get; }

    public GameViewRecordingRuntimeIdentity? ObservedRuntime { get; }

    public static GameViewRecordingCapabilityResolution Create (
        GameViewRecordingCapability capability,
        IpcGameViewRecordingStartBinding? startBinding,
        GameViewRecordingRuntimeIdentity? observedRuntime)
    {
        ArgumentNullException.ThrowIfNull(capability);

        if (capability.Package.State != GameViewRecordingPackageState.Resolved)
        {
            return Rejected(
                capability,
                observedRuntime,
                "Unity Recorder is not available in the resolved project packages.",
                GameViewRecordingErrorCodes.Unavailable);
        }

        if (capability.Compatibility.State != GameViewRecordingCompatibilityState.Supported)
        {
            return Rejected(
                capability,
                observedRuntime,
                "The resolved Unity Recorder version is not supported by this uCLI adapter.",
                GameViewRecordingErrorCodes.RecorderUnsupported);
        }

        if (capability.Adapter.State != GameViewRecordingAdapterState.Registered)
        {
            return Rejected(
                capability,
                observedRuntime,
                "The uCLI Unity Recorder adapter is not registered in the connected Editor.",
                GameViewRecordingErrorCodes.AdapterFaulted);
        }

        if (capability.RuntimeAdmission.State != GameViewRecordingRuntimeAdmissionState.Ready)
        {
            return Rejected(
                capability,
                observedRuntime,
                "The connected Unity Editor cannot currently start a GameView recording.",
                capability.RuntimeAdmission.BlockingCodes[0]);
        }

        return new ReadyGameViewRecordingAdmission(
            capability,
            startBinding ?? throw new ArgumentNullException(nameof(startBinding)),
            observedRuntime ?? throw new ArgumentNullException(nameof(observedRuntime)),
            capability.Limits
                ?? throw new ArgumentException("A ready recording capability must publish limits.", nameof(capability)),
            capability.CaptureProfile
                ?? throw new ArgumentException("A ready recording capability must publish a capture profile.", nameof(capability)));
    }

    private static RejectedGameViewRecordingAdmission Rejected (
        GameViewRecordingCapability capability,
        GameViewRecordingRuntimeIdentity? observedRuntime,
        string message,
        UcliCode code) =>
        new(
            capability,
            observedRuntime,
            ExecutionError.InternalError(message, code));
}

/// <summary>Contains every runtime value required to normalize and dispatch a recording start.</summary>
internal sealed record ReadyGameViewRecordingAdmission : GameViewRecordingCapabilityResolution
{
    public ReadyGameViewRecordingAdmission (
        GameViewRecordingCapability capability,
        IpcGameViewRecordingStartBinding startBinding,
        GameViewRecordingRuntimeIdentity observedRuntime,
        GameViewRecordingLimits limits,
        GameViewRecordingCaptureProfile captureProfile)
        : base(capability, observedRuntime)
    {
        StartBinding = startBinding ?? throw new ArgumentNullException(nameof(startBinding));
        if (StartBinding.Runtime != observedRuntime)
        {
            throw new ArgumentException(
                "The admitted start binding must identify the observed Unity runtime.",
                nameof(observedRuntime));
        }
        Limits = limits ?? throw new ArgumentNullException(nameof(limits));
        CaptureProfile = captureProfile ?? throw new ArgumentNullException(nameof(captureProfile));
    }

    public IpcGameViewRecordingStartBinding StartBinding { get; }

    public GameViewRecordingLimits Limits { get; }

    public GameViewRecordingCaptureProfile CaptureProfile { get; }
}

/// <summary>Contains the observed capability and the reason a recording start cannot be admitted.</summary>
internal sealed record RejectedGameViewRecordingAdmission : GameViewRecordingCapabilityResolution
{
    public RejectedGameViewRecordingAdmission (
        GameViewRecordingCapability capability,
        GameViewRecordingRuntimeIdentity? observedRuntime,
        ExecutionError error)
        : base(capability, observedRuntime)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public ExecutionError Error { get; }
}
