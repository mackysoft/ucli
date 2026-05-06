namespace MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

/// <summary> Identifies why a test-run path normalization operation failed. </summary>
internal enum TestRunPathNormalizationFailureKind
{
    None = 0,
    EmptyPath = 1,
    InvalidFormat = 2,
    OutsideRepositoryRoot = 3,
}
