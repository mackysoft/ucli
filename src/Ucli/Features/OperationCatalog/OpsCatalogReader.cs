using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;
using MackySoft.Ucli.UnityIntegration.Ipc;
using MackySoft.Ucli.UnityIntegration.Project;

namespace MackySoft.Ucli.Features.OperationCatalog;

/// <summary> Reads the operation catalog through the shared IPC execution path. </summary>
internal sealed class OpsCatalogReader : IOpsCatalogReader
{
    private readonly IUnityIpcRequestExecutor ipcRequestExecutor;

    /// <summary> Initializes a new instance of the <see cref="OpsCatalogReader" /> class. </summary>
    /// <param name="ipcRequestExecutor"> The shared IPC request executor dependency. </param>
    public OpsCatalogReader (IUnityIpcRequestExecutor ipcRequestExecutor)
    {
        this.ipcRequestExecutor = ipcRequestExecutor ?? throw new ArgumentNullException(nameof(ipcRequestExecutor));
    }

    /// <inheritdoc />
    public async ValueTask<OpsCatalogFetchResult> Read (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionMode mode,
        TimeSpan timeout,
        bool failFast,
        bool requireReadinessGate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var executionResult = await ipcRequestExecutor.Execute(
                UcliCommandIds.Ops,
                mode,
                timeout,
                config,
                project,
                IpcMethodNames.OpsRead,
                IpcPayloadCodec.SerializeToElement(new IpcOpsReadRequest(
                    FailFast: failFast,
                    RequireReadinessGate: requireReadinessGate)),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            return OpsCatalogFetchResult.Failure(
                executionResult.Message,
                executionResult.ErrorCode!);
        }

        return CreateResultFromResponse(executionResult.Response!, "ops.read");
    }

    private static OpsCatalogFetchResult CreateResultFromResponse (
        IpcResponse response,
        string responseSourceName)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrWhiteSpace(responseSourceName);

        if (IpcResponseFailureReader.TryRead(response, out var firstError, out var status))
        {
            if (firstError != null)
            {
                return OpsCatalogFetchResult.Failure(firstError.Message, firstError.Code);
            }

            return OpsCatalogFetchResult.Failure(
                $"{responseSourceName} failed with status '{status}'.",
                IpcErrorCodes.InternalError);
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcOpsReadResponse payload, out var payloadError))
        {
            return OpsCatalogFetchResult.Failure(
                $"{responseSourceName} payload is invalid. {payloadError.Message}",
                IpcErrorCodes.InternalError);
        }

        if (!IndexCatalogContractValidator.TryValidateOpsEntries(payload.Operations, "operations", out var validationError))
        {
            return OpsCatalogFetchResult.Failure(
                $"{responseSourceName} payload is invalid. {validationError}",
                IpcErrorCodes.InternalError);
        }

        return OpsCatalogFetchResult.Success(payload);
    }
}