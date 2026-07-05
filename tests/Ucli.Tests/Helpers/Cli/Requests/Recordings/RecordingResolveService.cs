using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;
using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve.Contracts;

namespace MackySoft.Tests;

internal sealed class RecordingResolveService : RecordingCommandService<ResolveCommandInput, ResolveServiceResult>, IResolveService
{
    public RecordingResolveService (Func<ResolveCommandInput, CancellationToken, ValueTask<ResolveServiceResult>> handler)
        : base(handler)
    {
    }

    public ValueTask<ResolveServiceResult> ExecuteAsync (
        ResolveCommandInput input,
        CancellationToken cancellationToken = default)
    {
        return ExecuteRecordedAsync(input, cancellationToken);
    }
}
