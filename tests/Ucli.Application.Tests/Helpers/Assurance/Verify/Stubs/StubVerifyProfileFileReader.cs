using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubVerifyProfileFileReader : IVerifyProfileFileReader
{
    private readonly Func<FilePathReference, AbsolutePath, VerifyProfileFileReadResult> resultFactory;

    public StubVerifyProfileFileReader (Func<FilePathReference, AbsolutePath, VerifyProfileFileReadResult> resultFactory)
    {
        this.resultFactory = resultFactory;
    }

    public ValueTask<VerifyProfileFileReadResult> ReadAsync (
        FilePathReference profilePath,
        AbsolutePath repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(resultFactory(profilePath, repositoryRoot));
    }
}
