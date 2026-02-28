namespace MackySoft.Ucli.TestProfile;

/// <summary> Executes profile-template initialization flow for <c>ucli test profile init</c>. </summary>
internal interface ITestProfileInitService
{
    /// <summary> Creates or overwrites a test profile template JSON file. </summary>
    /// <param name="outputPath"> The optional output path value from CLI input. </param>
    /// <param name="force"> Whether existing files can be overwritten. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the profile-init execution result that contains either generated output or a structured error. </returns>
    ValueTask<TestProfileInitExecutionResult> Execute (
        string? outputPath,
        bool force,
        CancellationToken cancellationToken = default);
}