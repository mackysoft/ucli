using MackySoft.Ucli.Application.Features.Testing.Profiles.Common.Contracts;

namespace MackySoft.Ucli.Application.Features.Testing.Profiles.UseCases.ProfileInit;

/// <summary> Executes profile-template initialization flow for <c>ucli test profile init</c>. </summary>
internal interface ITestProfileInitService
{
    /// <summary> Creates or overwrites a test profile template JSON file. </summary>
    /// <param name="input"> The normalized profile-init command input. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the profile-init execution result that contains either generated output or a structured error. </returns>
    ValueTask<TestProfileInitExecutionResult> Execute (
        TestProfileInitCommandInput input,
        CancellationToken cancellationToken = default);
}
