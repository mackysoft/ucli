using System;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Recording;
using MackySoft.Ucli.Unity.Recording;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>recording.capability</c> observations with or without Recorder. </summary>
    internal sealed class GameViewRecordingCapabilityUnityIpcMethodHandler : IUnityControlPlaneIpcMethodHandler
    {
        private readonly GameViewRecordingAdapterRegistry registry;

        private readonly GameViewRecordingIpcProjection projection;

        private readonly IGameViewRecorderPackageRegistry packageRegistry;

        public GameViewRecordingCapabilityUnityIpcMethodHandler (
            GameViewRecordingAdapterRegistry registry,
            GameViewRecordingIpcProjection projection,
            IGameViewRecorderPackageRegistry packageRegistry)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.projection = projection ?? throw new ArgumentNullException(nameof(projection));
            this.packageRegistry = packageRegistry ?? throw new ArgumentNullException(nameof(packageRegistry));
        }

        public UnityIpcMethod Method => UnityIpcMethod.RecordingCapability;

        public ValueTask<IpcResponse> HandleAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestCancellation cancellation)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodeGameViewRecordingCapabilityRequest(
                    request,
                    out IpcGameViewRecordingCapabilityRequest _,
                    out var decodeError))
            {
                return new ValueTask<IpcResponse>(decodeError);
            }

            IpcGameViewRecordingCapabilityResponse payload;
            if (registry.TryGet(out var adapter))
            {
                var admission = adapter.GetRuntimeAdmission();
                var observedRuntime = TryCaptureObservedRuntime();
                var startBinding = admission is GameViewRecordingRuntimeReadyAdmission
                    ? projection.CaptureCurrentBinding()
                    : null;
                payload = new IpcGameViewRecordingCapabilityResponse(
                    new GameViewRecordingAdapterCapability(
                        GameViewRecordingAdapterState.Registered,
                        adapter.Metadata.AdapterId,
                        adapter.Metadata.AdapterVersion),
                    ProjectAdmission(admission),
                    adapter.Metadata.Limits,
                    adapter.Metadata.CaptureProfile,
                    startBinding,
                    startBinding?.Runtime ?? observedRuntime);
            }
            else
            {
                payload = CreateUnavailableCapability();
            }

            return new ValueTask<IpcResponse>(
                UnityIpcResponseFactory.CreateSuccessResponse(request, payload));
        }

        private IpcGameViewRecordingCapabilityResponse CreateUnavailableCapability ()
        {
            try
            {
                if (!packageRegistry.TryGetRecorderPackageVersion(out var packageVersion))
                {
                    return CreateBlockedUnavailable(
                        GameViewRecordingAdapterState.NotApplicable,
                        GameViewRecordingErrorCodes.Unavailable);
                }

                if (!IsSupportedRecorderVersion(packageVersion))
                {
                    return CreateBlockedUnavailable(
                        GameViewRecordingAdapterState.NotApplicable,
                        GameViewRecordingErrorCodes.RecorderUnsupported);
                }

                return CreateBlockedUnavailable(
                    GameViewRecordingAdapterState.Missing,
                    GameViewRecordingErrorCodes.AdapterFaulted);
            }
            catch
            {
                return CreateUnobservedUnavailable(
                    GameViewRecordingAdapterState.Unobserved,
                    GameViewRecordingErrorCodes.AdapterFaulted);
            }
        }

        private IpcGameViewRecordingCapabilityResponse CreateBlockedUnavailable (
            GameViewRecordingAdapterState adapterState,
            UcliCode code)
        {
            return CreateUnavailable(
                adapterState,
                GameViewRecordingRuntimeAdmissionState.Blocked,
                code);
        }

        private IpcGameViewRecordingCapabilityResponse CreateUnobservedUnavailable (
            GameViewRecordingAdapterState adapterState,
            UcliCode code)
        {
            return CreateUnavailable(
                adapterState,
                GameViewRecordingRuntimeAdmissionState.Unobserved,
                code);
        }

        private IpcGameViewRecordingCapabilityResponse CreateUnavailable (
            GameViewRecordingAdapterState adapterState,
            GameViewRecordingRuntimeAdmissionState runtimeAdmissionState,
            UcliCode code)
        {
            return new IpcGameViewRecordingCapabilityResponse(
                new GameViewRecordingAdapterCapability(adapterState, null, null),
                new MackySoft.Ucli.Contracts.Recording.GameViewRecordingRuntimeAdmission(
                    runtimeAdmissionState,
                    new[] { code }),
                limits: null,
                captureProfile: null,
                startBinding: null,
                observedRuntime: TryCaptureObservedRuntime());
        }

        private GameViewRecordingRuntimeIdentity? TryCaptureObservedRuntime ()
        {
            try
            {
                return projection.CaptureCurrentBinding().Runtime;
            }
            catch
            {
                return null;
            }
        }

        private static MackySoft.Ucli.Contracts.Recording.GameViewRecordingRuntimeAdmission ProjectAdmission (
            MackySoft.Ucli.Unity.Recording.GameViewRecordingRuntimeAdmission admission) =>
            admission switch
            {
                GameViewRecordingRuntimeReadyAdmission => new MackySoft.Ucli.Contracts.Recording.GameViewRecordingRuntimeAdmission(
                    GameViewRecordingRuntimeAdmissionState.Ready,
                    Array.Empty<UcliCode>()),
                GameViewRecordingRuntimeRejectedAdmission rejected => new MackySoft.Ucli.Contracts.Recording.GameViewRecordingRuntimeAdmission(
                    GameViewRecordingRuntimeAdmissionState.Blocked,
                    new[] { GameViewRecordingIpcProjection.MapErrorCode(rejected.Failure) }),
                _ => throw new ArgumentOutOfRangeException(nameof(admission)),
            };

        private static bool TryParseVersion (string value, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var suffixIndex = value.IndexOf('-');
            var core = suffixIndex < 0 ? value : value.Substring(0, suffixIndex);
            return Version.TryParse(core, out version);
        }

        private static bool IsSupportedRecorderVersion (string packageVersion)
        {
            var range = GameViewRecorderCompatibilityMetadata.RecorderPackageVersionRange;
            var separator = range.IndexOf(',');
            if (range.Length < 6
                || range[0] != '['
                || range[range.Length - 1] != ')'
                || separator <= 1
                || !TryParseVersion(packageVersion, out var version)
                || !Version.TryParse(range.Substring(1, separator - 1), out var minimum)
                || !Version.TryParse(
                    range.Substring(separator + 1, range.Length - separator - 2),
                    out var maximumExclusive))
            {
                return false;
            }

            return version >= minimum && version < maximumExclusive;
        }
    }
}
