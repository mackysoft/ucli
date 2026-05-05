using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call.Projection;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;

namespace MackySoft.Ucli.Hosting.Cli.Requests.Call.Preflight;

/// <summary> Prepares base payload data for <c>call</c> command failures that occur after option parsing. </summary>
internal sealed class CallCommandPreflightService : ICallCommandPreflightService
{
    private readonly IRequestPreparationService requestPreparationService;

    /// <summary> Initializes a new instance of the <see cref="CallCommandPreflightService" /> class. </summary>
    public CallCommandPreflightService (IRequestPreparationService requestPreparationService)
    {
        this.requestPreparationService = requestPreparationService ?? throw new ArgumentNullException(nameof(requestPreparationService));
    }

    /// <inheritdoc />
    public async ValueTask<CallCommandPreflightResult> Prepare (
        string? projectPath,
        string requestJson,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var requestPreparationResult = await requestPreparationService.Prepare(
                projectPath,
                requestJson,
                cancellationToken)
            .ConfigureAwait(false);
        var output = CallExecutionOutputFactory.CreateBase(requestPreparationResult.PreparedRequest?.Request.RequestId);
        if (requestPreparationResult.Error != null)
        {
            return CallCommandPreflightResult.Failure(
                CallFailureResultFactory.FromExecutionError(requestPreparationResult.Error, output));
        }

        return CallCommandPreflightResult.Success(
            output ?? throw new InvalidOperationException("Call preflight must produce a base payload when it succeeds."));
    }
}
