using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.TestRun.Configuration;

/// <summary> Represents the result of loading one test-run profile file. </summary>
/// <param name="Profile"> The loaded profile on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured load error on failure; otherwise <see langword="null" />. </param>
internal sealed record TestRunProfileLoadResult (
    TestRunProfile? Profile,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether profile loading succeeded. </summary>
    public bool IsSuccess => Profile is not null && Error is null;

    /// <summary> Creates a successful profile-load result. </summary>
    /// <param name="profile"> The loaded profile. </param>
    /// <returns> The successful result. </returns>
    public static TestRunProfileLoadResult Success (TestRunProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new TestRunProfileLoadResult(profile, null);
    }

    /// <summary> Creates a failed profile-load result. </summary>
    /// <param name="error"> The structured load error. </param>
    /// <returns> The failed result. </returns>
    public static TestRunProfileLoadResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new TestRunProfileLoadResult(null, error);
    }
}