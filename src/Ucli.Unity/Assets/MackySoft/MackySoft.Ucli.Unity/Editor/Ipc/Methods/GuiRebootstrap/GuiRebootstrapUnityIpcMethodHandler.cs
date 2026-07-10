using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>gui.rebootstrap</c> IPC method requests. </summary>
    internal sealed class GuiRebootstrapUnityIpcMethodHandler : IUnityControlPlaneIpcMethodHandler
    {
        private readonly string projectFingerprint;

        private readonly IDaemonLogger daemonLogger;

        private readonly IUnityGuiBootstrapStarter bootstrapStarter;

        /// <summary> Initializes a new instance of the <see cref="GuiRebootstrapUnityIpcMethodHandler" /> class. </summary>
        /// <param name="bootstrapStarter"> The GUI bootstrap starter dependency. </param>
        /// <param name="projectFingerprint"> The project fingerprint served by this IPC host. </param>
        /// <param name="daemonLogger"> The daemon logger dependency. </param>
        public GuiRebootstrapUnityIpcMethodHandler (
            IUnityGuiBootstrapStarter bootstrapStarter,
            string projectFingerprint,
            IDaemonLogger daemonLogger)
        {
            this.bootstrapStarter = bootstrapStarter ?? throw new ArgumentNullException(nameof(bootstrapStarter));
            if (string.IsNullOrWhiteSpace(projectFingerprint))
            {
                throw new ArgumentException("Project fingerprint must not be empty.", nameof(projectFingerprint));
            }

            this.projectFingerprint = projectFingerprint;
            this.daemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
        }

        /// <inheritdoc />
        public string Method => IpcMethodNames.GuiRebootstrap;

        /// <inheritdoc />
        public async ValueTask<IpcResponse> HandleAsync (
            IpcRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!IpcPayloadCodec.TryDeserialize(
                    request.Payload,
                    out IpcGuiRebootstrapRequest payload,
                    out var readError))
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    $"GUI rebootstrap payload is invalid. {readError.Message}",
                    null);
            }

            if (!string.Equals(payload.ProjectFingerprint, projectFingerprint, StringComparison.Ordinal))
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    "GUI rebootstrap projectFingerprint does not match this Editor process.",
                    null);
            }

            try
            {
                var sessionReplacementScope = payload.ReplaceExistingSession
                    ? UnityGuiSessionReplacementScope.AnyCurrentProcessSession
                    : UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession;
                var startResult = await bootstrapStarter.StartAsync(
                    bootstrapArguments: null,
                    sessionReplacementScope: sessionReplacementScope,
                    cancellationToken: cancellationToken);
                if (!startResult.IsSuccess)
                {
                    daemonLogger.Warning(
                        DaemonLogCategories.Lifecycle,
                        $"GUI daemon rebootstrap request rejected. {startResult.ErrorMessage}");
                    return UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        UcliCoreErrorCodes.InternalError,
                        $"GUI rebootstrap failed. {startResult.ErrorMessage}",
                        null);
                }

                daemonLogger.Info(
                    DaemonLogCategories.Lifecycle,
                    "GUI daemon rebootstrap request accepted.");
                using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                var response = new IpcGuiRebootstrapResponse(
                    Accepted: true,
                    ProjectFingerprint: projectFingerprint,
                    ProcessId: currentProcess.Id);
                return UnityIpcResponseFactory.CreateSuccessResponse(request, response);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                daemonLogger.Exception(
                    DaemonLogCategories.Lifecycle,
                    "GUI daemon rebootstrap request failed.",
                    exception);
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    $"GUI rebootstrap failed. {exception.Message}",
                    null);
            }
        }
    }

    internal interface IUnityGuiBootstrapStarter
    {
        Task<UnityGuiBootstrapStartResult> StartAsync (
            IpcGuiBootstrapArguments bootstrapArguments,
            UnityGuiSessionReplacementScope sessionReplacementScope,
            CancellationToken cancellationToken);
    }

    internal sealed class UnityGuiBootstrapStarter : IUnityGuiBootstrapStarter
    {
        public Task<UnityGuiBootstrapStartResult> StartAsync (
            IpcGuiBootstrapArguments bootstrapArguments,
            UnityGuiSessionReplacementScope sessionReplacementScope,
            CancellationToken cancellationToken)
        {
            return UnityGuiBootstrap.StartAsync(
                bootstrapArguments: bootstrapArguments,
                sessionReplacementScope: sessionReplacementScope,
                cancellationToken: cancellationToken);
        }
    }
}
