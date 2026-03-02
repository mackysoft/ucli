namespace MackySoft.Ucli.TestRun.Configuration;

/// <summary> Loads one test-run profile JSON file from disk. </summary>
internal interface ITestRunProfileLoader
{
    /// <summary> Loads one profile from the specified path. </summary>
    /// <param name="profilePath"> The profile path value. </param>
    /// <returns> The profile load result. </returns>
    TestRunProfileLoadResult Load (string profilePath);
}