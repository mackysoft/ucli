using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Input;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubVerifyFromInputFileReader : IVerifyFromInputFileReader
{
    private readonly Func<FilePathReference, AbsolutePath, VerifyFromInputFileReadResult> resultFactory;

    public StubVerifyFromInputFileReader (Func<FilePathReference, AbsolutePath, VerifyFromInputFileReadResult> resultFactory)
    {
        this.resultFactory = resultFactory;
    }

    public ValueTask<VerifyFromInputFileReadResult> ReadAsync (
        FilePathReference fromPath,
        AbsolutePath repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(resultFactory(fromPath, repositoryRoot));
    }
}
