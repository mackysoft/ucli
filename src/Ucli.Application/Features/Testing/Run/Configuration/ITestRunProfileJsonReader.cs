namespace MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

/// <summary> Reads raw JSON text for one test-run profile from an external source. </summary>
internal interface ITestRunProfileJsonReader
{
    /// <summary> Reads one profile JSON text value from the specified path. </summary>
    /// <param name="profilePath"> The profile path value. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the profile JSON read result. </returns>
    ValueTask<TestRunProfileJsonReadResult> ReadTextAsync (
        string profilePath,
        CancellationToken cancellationToken = default);
}
