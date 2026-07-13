using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Features.OperationCatalog.Catalog.Source;

/// <summary> Reads the operation catalog through the shared IPC execution path. </summary>
internal sealed class OpsCatalogReader : IOpsCatalogReader
{
    private readonly IUnityRequestExecutor ipcRequestExecutor;

    /// <summary> Initializes a new instance of the <see cref="OpsCatalogReader" /> class. </summary>
    /// <param name="ipcRequestExecutor"> The shared IPC request executor dependency. </param>
    public OpsCatalogReader (IUnityRequestExecutor ipcRequestExecutor)
    {
        this.ipcRequestExecutor = ipcRequestExecutor ?? throw new ArgumentNullException(nameof(ipcRequestExecutor));
    }

    /// <inheritdoc />
    public async ValueTask<OpsCatalogFetchResult> ReadAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionMode mode,
        TimeSpan timeout,
        bool failFast,
        bool requireReadinessGate,
        bool includeEditLoweringOnly = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var executionResult = await ipcRequestExecutor.ExecuteAsync(
                UcliCommandIds.Ops,
                mode,
                timeout,
                config,
                project,
                new UnityRequestPayload.OpsRead(
                    FailFast: failFast,
                    RequireReadinessGate: requireReadinessGate,
                    IncludeEditLoweringOnly: includeEditLoweringOnly),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            return OpsCatalogFetchResult.Failure(
                executionResult.Message,
                executionResult.ErrorCode!.Value,
                executionResult.FailureInfo!.StartupFailure);
        }

        return CreateResultFromResponse(executionResult.Response!, "ops.read", includeEditLoweringOnly);
    }

    private static OpsCatalogFetchResult CreateResultFromResponse (
        UnityRequestResponse response,
        string responseSourceName,
        bool allowEditLoweringOnlyEntries)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrWhiteSpace(responseSourceName);

        if (response.HasFailureStatus || response.Errors.Count != 0)
        {
            var firstError = response.Errors.FirstOrDefault();
            if (firstError != null)
            {
                return OpsCatalogFetchResult.Failure(firstError.Message, firstError.Code);
            }

            if (!string.IsNullOrWhiteSpace(response.FailureStatus))
            {
                return OpsCatalogFetchResult.Failure(
                    $"{responseSourceName} failed with status '{response.FailureStatus}'.",
                    UcliCoreErrorCodes.InternalError);
            }

            return OpsCatalogFetchResult.Failure(
                $"{responseSourceName} failed with an error status.",
                UcliCoreErrorCodes.InternalError);
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcOpsReadResponse payload, out var payloadError))
        {
            return OpsCatalogFetchResult.Failure(
                $"{responseSourceName} payload is invalid. {payloadError.Message}",
                UcliCoreErrorCodes.InternalError);
        }

        if (!OpsCatalogSnapshot.TryCreate(
                payload.GeneratedAtUtc,
                payload.Operations,
                "operations",
                allowEditLoweringOnlyEntries,
                out var snapshot,
                out var validationError))
        {
            return OpsCatalogFetchResult.Failure(
                $"{responseSourceName} payload is invalid. {validationError}",
                UcliCoreErrorCodes.InternalError);
        }

        return OpsCatalogFetchResult.Success(snapshot!);
    }
}
