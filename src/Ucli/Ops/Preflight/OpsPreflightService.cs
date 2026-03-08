using MackySoft.Ucli.Context;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.ReadIndex;

namespace MackySoft.Ucli.Ops.Preflight;

/// <summary> Implements preflight flow for context resolution and read-index mode resolution. </summary>
internal sealed class OpsPreflightService : IOpsPreflightService
{
    private readonly IInitStatusContextResolver initStatusContextResolver;

    /// <summary> Initializes a new instance of the <see cref="OpsPreflightService" /> class. </summary>
    /// <param name="initStatusContextResolver"> The shared init/status context resolver dependency. </param>
    public OpsPreflightService (IInitStatusContextResolver initStatusContextResolver)
    {
        this.initStatusContextResolver = initStatusContextResolver ?? throw new ArgumentNullException(nameof(initStatusContextResolver));
    }

    /// <inheritdoc />
    public async ValueTask<OpsPreflightResult> Execute (
        OpsCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        var contextResult = await initStatusContextResolver.Resolve(
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

        return OpsPreflightResult.Success(new OpsPreflightContext(context, readIndexModeResult.Mode!.Value));
    }

    private static OpsPreflightResult FromExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return OpsPreflightResult.Failure(
            error.Message,
            ExecutionErrorKindCodeMapper.ToCode(error.Kind));
    }
}