using MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubVerifyProfileFileReader : IVerifyProfileFileReader
{
    private readonly Func<string, string, VerifyProfileFileReadResult> resultFactory;

    public StubVerifyProfileFileReader (Func<string, string, VerifyProfileFileReadResult> resultFactory)
    {
        this.resultFactory = resultFactory;
    }

    public ValueTask<VerifyProfileFileReadResult> ReadAsync (
        string profilePath,
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(resultFactory(profilePath, repositoryRoot));
    }
}
