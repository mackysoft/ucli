using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Context;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;

namespace MackySoft.Ucli.Features.OperationCatalog.Preflight;

/// <summary> Implements preflight flow for context resolution and read-index mode resolution. </summary>
internal sealed class OpsPreflightService : IOpsPreflightService
{
    private readonly IProjectContextResolver projectContextResolver;

    /// <summary> Initializes a new instance of the <see cref="OpsPreflightService" /> class. </summary>
    /// <param name="projectContextResolver"> The shared project-context resolver dependency. </param>
    public OpsPreflightService (IProjectContextResolver projectContextResolver)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
    }

    /// <inheritdoc />
    public async ValueTask<OpsPreflightResult> Execute (
        OpsCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        var contextResult = await projectContextResolver.Resolve(
                input.ProjectPath,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return FromExecutionError(contextResult.Error!);
        }

        var context = contextResult.Context!;
        var readIndexModeResult = ReadIndexModeResolver.Resolve(input.ReadIndexMode, context.Config);
        if (!readIndexModeResult.IsSuccess)
        {
            return FromExecutionError(readIndexModeResult.Error!);
        }

        var timeoutResolutionResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.Ops,
            context.Config);
        if (!timeoutResolutionResult.IsSuccess)
        {
            return FromExecutionError(timeoutResolutionResult.Error!);
        }

        return OpsPreflightResult.Success(
            new OpsPreflightContext(
                context,
                readIndexModeResult.Mode!.Value,
                input.Mode ?? UnityExecutionMode.Auto,
                timeoutResolutionResult.Timeout!.Value,
                input.FailFast));
    }

    private static OpsPreflightResult FromExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return OpsPreflightResult.Failure(
            error.Message,
            ExecutionErrorCodeMapper.ToCode(error.Kind));
    }
}