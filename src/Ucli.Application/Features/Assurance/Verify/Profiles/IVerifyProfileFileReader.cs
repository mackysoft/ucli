using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;

/// <summary> Reads repository-local verify profile JSON for the application use case. </summary>
internal interface IVerifyProfileFileReader
{
    /// <summary> Reads one repository-local profile file. </summary>
    ValueTask<VerifyProfileFileReadResult> ReadAsync (
        string profilePath,
        AbsolutePath repositoryRoot,
        CancellationToken cancellationToken = default);
}
