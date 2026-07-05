using MackySoft.Ucli.Application.Features.Testing.Profiles.Common.Contracts;
using MackySoft.Ucli.Application.Features.Testing.Profiles.UseCases.ProfileInit;

namespace MackySoft.Tests;

internal sealed class RecordingTestProfileInitService : RecordingCommandService<TestProfileInitCommandInput, TestProfileInitExecutionResult>, ITestProfileInitService
{
    public RecordingTestProfileInitService (Func<TestProfileInitCommandInput, CancellationToken, ValueTask<TestProfileInitExecutionResult>> handler)
        : base(handler)
    {
    }

    public ValueTask<TestProfileInitExecutionResult> ExecuteAsync (
        TestProfileInitCommandInput input,
        CancellationToken cancellationToken = default)
    {
        return ExecuteRecordedAsync(input, cancellationToken);
    }
}
