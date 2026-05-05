namespace MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

/// <summary> Loads one test-run profile from raw external profile JSON. </summary>
internal interface ITestRunProfileLoader
{
    /// <summary> Loads one profile from the specified profile source path. </summary>
    /// <param name="profilePath"> The profile path value. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the profile load result. </returns>
    ValueTask<TestRunProfileLoadResult> LoadAsync (
        string profilePath,
        CancellationToken cancellationToken = default);
}
