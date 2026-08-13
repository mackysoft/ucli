using System.Text.Json;
using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Programs.Supervision;

namespace MackySoft.Ucli.Application.Tests.Features.Programs.Supervision;

public sealed class ProgramGuiRequirementTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Find_ScansTheEntireDefinitionAndReturnsTheFirstGuiStep ()
    {
        using var document = JsonDocument.Parse("{\"steps\":[]}");
        var definition = new ProgramDefinition(
            [new RefreshProgramStep(null), new ScreenshotSceneProgramStep(null), new PlayEnterProgramStep(null)],
            document.RootElement.Clone());

        var requirement = ProgramGuiRequirement.Find(definition);

        Assert.NotNull(requirement);
        Assert.Equal(1, requirement.StepIndex);
        Assert.Equal("screenshot.scene", requirement.Command);
        Assert.Equal("/steps/1", requirement.InstancePath);
        Assert.Equal(ScreenshotErrorCodes.ScreenshotRequiresGuiSession, requirement.ErrorCode);
    }
}
