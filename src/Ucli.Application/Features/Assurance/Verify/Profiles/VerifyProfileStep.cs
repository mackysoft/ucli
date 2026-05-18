using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;

/// <summary> Represents one canonical verify profile step. </summary>
internal sealed record VerifyProfileStep (
    string Kind,
    bool Required,
    IReadOnlyList<string> Effects)
{
    /// <summary> Gets the ready target for ready verifier steps. </summary>
    public ReadyTarget ReadyTarget { get; init; } = ReadyTarget.Execution;

    /// <summary> Gets the optional test platform for test verifier steps. </summary>
    public TestRunPlatform? TestPlatform { get; init; }

    /// <summary> Gets the optional test filter for test verifier steps. </summary>
    public string? TestFilter { get; init; }

    /// <summary> Gets the optional test categories for test verifier steps. </summary>
    public string[]? TestCategory { get; init; }

    /// <summary> Gets the optional assembly names for test verifier steps. </summary>
    public string[]? AssemblyName { get; init; }
}
