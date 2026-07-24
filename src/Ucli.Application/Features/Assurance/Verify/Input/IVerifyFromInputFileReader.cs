using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Input;

/// <summary> Reads public uCLI result JSON for <c>verify --from</c>. </summary>
internal interface IVerifyFromInputFileReader
{
    /// <summary> Reads one repository-local <c>--from</c> JSON file. </summary>
    ValueTask<VerifyFromInputFileReadResult> ReadAsync (
        string fromPath,
        AbsolutePath repositoryRoot,
        CancellationToken cancellationToken = default);
}
