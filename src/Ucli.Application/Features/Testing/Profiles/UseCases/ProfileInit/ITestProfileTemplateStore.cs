using MackySoft.Ucli.Application.Features.Testing.Profiles.Common.Contracts;

namespace MackySoft.Ucli.Application.Features.Testing.Profiles.UseCases.ProfileInit;

/// <summary> Persists test-profile templates through a host-owned storage adapter. </summary>
internal interface ITestProfileTemplateStore
{
    /// <summary> Writes one test-profile template and returns the generated path or a structured storage error. </summary>
    /// <param name="profile"> The default profile template to persist. </param>
    /// <param name="outputPath"> The optional output path requested by the command. </param>
    /// <param name="force"> Whether an existing profile file can be overwritten. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the profile initialization result. </returns>
    ValueTask<TestProfileInitExecutionResult> WriteAsync (
        TestProfile profile,
        string? outputPath,
        bool force,
        CancellationToken cancellationToken = default);
}
