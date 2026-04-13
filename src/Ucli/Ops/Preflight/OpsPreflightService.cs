using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.ReadIndex;

namespace MackySoft.Ucli.Ops.Preflight;

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

        var executionModeResult = UnityExecutionModeResolver.Resolve(input.Mode);
        if (!executionModeResult.IsSuccess)
        {
            return FromExecutionError(executionModeResult.Error!);
        }

        var timeoutResolutionResult = IpcCommandTimeoutResolver.Resolve(
            input.Timeout,
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
                executionModeResult.Mode!.Value,
                timeoutResolutionResult.Timeout!.Value));
    }

    private static OpsPreflightResult FromExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return OpsPreflightResult.Failure(
            error.Message,
            ExecutionErrorKindCodeMapper.ToCode(error.Kind));
    }
}