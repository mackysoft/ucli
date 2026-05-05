namespace MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

/// <summary> Probes filesystem path existence required by test-run configuration validation. </summary>
internal interface ITestRunPathExistenceProbe
{
    /// <summary> Determines whether one file path exists. </summary>
    /// <param name="path"> The file path to probe. </param>
    /// <returns> <see langword="true" /> when the file exists; otherwise <see langword="false" />. </returns>
    bool FileExists (string path);
}
