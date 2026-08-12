using System.Text.Json;
using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Programs.Planning;

namespace MackySoft.Ucli.Application.Tests.Features.Programs.Planning;

public sealed class ProgramPlanProjectionTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_ProjectsOnlyTheCurrentFrontierAndDefersTheNextGeneration ()
    {
        using var document = JsonDocument.Parse("""
            { "steps": [
              { "command": "ready" },
              { "command": "compile" },
              { "command": "screenshot.game" }
            ] }
            """);
        var definition = new ProgramDefinition(
            [new ReadyProgramStep(null), new CompileProgramStep(null), new ScreenshotGameProgramStep(null, null, null)],
            document.RootElement.Clone());

        var plan = ProgramPlanProjection.Create(definition, startIndex: 0);

        Assert.Equal(
            [ProgramPlanStepState.Current, ProgramPlanStepState.Current, ProgramPlanStepState.Deferred],
            plan.Steps.Select(static step => step.State));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_DoesNotResolveOrExecuteFutureSteps ()
    {
        using var document = JsonDocument.Parse("{\"steps\":[{\"command\":\"refresh\"},{\"command\":\"call\",\"steps\":[]}]} ");
        var definition = new ProgramDefinition(
            [new RefreshProgramStep(null), new InlineCallProgramStep(null, null!)],
            document.RootElement.Clone());

        var plan = ProgramPlanProjection.Create(definition, startIndex: 0);

        Assert.Equal(ProgramPlanStepState.Current, plan.Steps[0].State);
        Assert.Equal(ProgramPlanStepState.Deferred, plan.Steps[1].State);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_FromANonzeroIndexProjectsThroughOnlyTheNextLifecycleBoundary ()
    {
        using var document = JsonDocument.Parse("{\"steps\":[{\"command\":\"ready\"},{\"command\":\"call\",\"steps\":[]},{\"command\":\"compile\"},{\"command\":\"ready\"}]} ");
        var steps = new ProgramStep[]
        {
            new ReadyProgramStep(null), new InlineCallProgramStep(null, null!), new CompileProgramStep(null), new ReadyProgramStep(null),
        };
        var definition = new ProgramDefinition(steps, document.RootElement.Clone());

        var plan = ProgramPlanProjection.Create(definition, startIndex: 1);

        Assert.Equal([ProgramPlanStepState.Deferred, ProgramPlanStepState.Current, ProgramPlanStepState.Current, ProgramPlanStepState.Deferred],
            plan.Steps.Select(static step => step.State));
        Assert.Equal(["ready", "call", "compile", "ready"], plan.Steps.Select(static step => step.Command));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_FromAConsecutiveLifecycleBoundaryProjectsOnlyItsFirstBoundary ()
    {
        using var document = JsonDocument.Parse("{\"steps\":[{\"command\":\"ready\"},{\"command\":\"refresh\"},{\"command\":\"compile\"},{\"command\":\"ready\"}]} ");
        var definition = new ProgramDefinition(
            [new ReadyProgramStep(null), new RefreshProgramStep(null), new CompileProgramStep(null), new ReadyProgramStep(null)],
            document.RootElement.Clone());

        var plan = ProgramPlanProjection.Create(definition, startIndex: 1);

        Assert.Equal([ProgramPlanStepState.Deferred, ProgramPlanStepState.Current, ProgramPlanStepState.Deferred, ProgramPlanStepState.Deferred],
            plan.Steps.Select(static step => step.State));
        Assert.Equal(["ready", "refresh", "compile", "ready"], plan.Steps.Select(static step => step.Command));
    }
}
