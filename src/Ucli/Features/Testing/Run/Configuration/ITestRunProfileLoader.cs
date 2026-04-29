namespace MackySoft.Ucli.Features.Testing.Run.Configuration;

/// <summary> Loads one test-run profile JSON file from disk. </summary>
internal interface ITestRunProfileLoader
{
    /// <summary> Loads one profile from the specified path. </summary>
    /// <param name="profilePath"> The profile path value. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the profile load result. </returns>
    ValueTask<TestRunProfileLoadResult> Load (
        string profilePath,
        CancellationToken cancellationToken = default);
}
