using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>gui.rebootstrap</c> IPC method requests. </summary>
    internal sealed class GuiRebootstrapUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly string projectFingerprint;

        private readonly IDaemonLogger daemonLogger;

        public GuiRebootstrapUnityIpcMethodHandler (
            string projectFingerprint,
            IDaemonLogger daemonLogger = null)
        {
            if (string.IsNullOrWhiteSpace(projectFingerprint))
            {
                throw new ArgumentException("Project fingerprint must not be empty.", nameof(projectFingerprint));
            }

            this.projectFingerprint = projectFingerprint;
            this.daemonLogger = daemonLogger ?? NoOpDaemonLogger.Instance;
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
                var startResult = await UnityGuiBootstrap.StartAsync(null);
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
            catch (Exception exception)
            {
                daemonLogger.Exception(
                    DaemonLogCategories.Lifecycle,
                    "GUI daemon rebootstrap request failed.",
                    exception);
                Debug.LogException(exception);
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    $"GUI rebootstrap failed. {exception.Message}",
                    null);
            }
        }
    }
}
