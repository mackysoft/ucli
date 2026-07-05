using MackySoft.Ucli.Application.Features.Requests.Validate.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Validate.UseCases.Validate;

namespace MackySoft.Tests;

internal sealed class RecordingValidateService : RecordingCommandService<ValidateCommandInput, ValidateServiceResult>, IValidateService
{
    public RecordingValidateService (Func<ValidateCommandInput, CancellationToken, ValueTask<ValidateServiceResult>> handler)
        : base(handler)
    {
    }

    public ValueTask<ValidateServiceResult> ExecuteAsync (
        ValidateCommandInput input,
        CancellationToken cancellationToken = default)
    {
        return ExecuteRecordedAsync(input, cancellationToken);
    }
}
