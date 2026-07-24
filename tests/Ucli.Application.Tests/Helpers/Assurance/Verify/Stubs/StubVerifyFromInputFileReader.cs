using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Input;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubVerifyFromInputFileReader : IVerifyFromInputFileReader
{
    private readonly Func<string, AbsolutePath, VerifyFromInputFileReadResult> resultFactory;

    public StubVerifyFromInputFileReader (Func<string, AbsolutePath, VerifyFromInputFileReadResult> resultFactory)
    {
        this.resultFactory = resultFactory;
    }

    public ValueTask<VerifyFromInputFileReadResult> ReadAsync (
        string fromPath,
        AbsolutePath repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(resultFactory(fromPath, repositoryRoot));
    }
}
