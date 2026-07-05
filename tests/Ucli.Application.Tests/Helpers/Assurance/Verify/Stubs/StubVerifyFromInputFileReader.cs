using MackySoft.Ucli.Application.Features.Assurance.Verify.Input;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubVerifyFromInputFileReader : IVerifyFromInputFileReader
{
    private readonly Func<string, string, VerifyFromInputFileReadResult> resultFactory;

    public StubVerifyFromInputFileReader (Func<string, string, VerifyFromInputFileReadResult> resultFactory)
    {
        this.resultFactory = resultFactory;
    }

    public ValueTask<VerifyFromInputFileReadResult> ReadAsync (
        string fromPath,
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(resultFactory(fromPath, repositoryRoot));
    }
}
