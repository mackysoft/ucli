using MackySoft.Ucli.Application.Features.Programs.Persistence;

namespace MackySoft.Ucli.Tests.Features.Programs.Persistence;

public sealed class ProgramRunTerminalPublicationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TerminalArtifactContract_UsesProgramOwnedImmutableKinds ()
    {
        Assert.Equal("programRunTerminalRecord", ProgramTerminalArtifactContract.RunTerminalRecordKind.Value);
        Assert.Equal("programStepTerminalRecord", ProgramTerminalArtifactContract.StepTerminalRecordKind.Value);
    }
}
