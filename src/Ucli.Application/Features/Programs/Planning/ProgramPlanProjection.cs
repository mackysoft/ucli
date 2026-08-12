using MackySoft.Ucli.Application.Features.Programs.Parsing;

namespace MackySoft.Ucli.Application.Features.Programs.Planning;

/// <summary>
/// Projects the currently executable Program segment without resolving or retaining
/// work that belongs to a later Editor execution generation.
/// </summary>
internal static class ProgramPlanProjection
{
    /// <summary> Creates a pure projection for the segment beginning at <paramref name="startIndex" />. </summary>
    public static ProgramPlanProjectionResult Create (ProgramDefinition definition, int startIndex)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (startIndex < 0 || startIndex >= definition.Steps.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        }

        var frontierEnd = FindFrontierEnd(definition.Steps, startIndex);
        var steps = definition.Steps.Select((step, index) => new ProgramPlanStepProjection(
            index,
            GetCommand(step),
            index >= startIndex && index <= frontierEnd
                ? ProgramPlanStepState.Current
                : ProgramPlanStepState.Deferred)).ToArray();
        return new ProgramPlanProjectionResult(steps);
    }

    private static int FindFrontierEnd (IReadOnlyList<ProgramStep> steps, int startIndex)
    {
        for (var index = startIndex; index < steps.Count; index++)
        {
            if (IsGenerationBoundary(steps[index]))
            {
                return index;
            }
        }

        return steps.Count - 1;
    }

    private static bool IsGenerationBoundary (ProgramStep step) => step is RefreshProgramStep
        or CompileProgramStep
        or PlayEnterProgramStep
        or PlayExitProgramStep;

    private static string GetCommand (ProgramStep step) => step switch
    {
        CallProgramStep => "call",
        ReadyProgramStep => "ready",
        RefreshProgramStep => "refresh",
        CompileProgramStep => "compile",
        PlayEnterProgramStep => "play.enter",
        PlayExitProgramStep => "play.exit",
        ScreenshotGameProgramStep => "screenshot.game",
        ScreenshotSceneProgramStep => "screenshot.scene",
        _ => throw new ArgumentOutOfRangeException(nameof(step), "Program Step is not supported."),
    };
}

/// <summary> Represents the read-only Program Plan view for one current execution frontier. </summary>
internal sealed record ProgramPlanProjectionResult (IReadOnlyList<ProgramPlanStepProjection> Steps);

/// <summary> Represents one input Program Step in a Program Plan view. </summary>
internal sealed record ProgramPlanStepProjection (int Index, string Command, ProgramPlanStepState State);

/// <summary> Distinguishes the current executable frontier from later deferred work. </summary>
internal enum ProgramPlanStepState
{
    Current = 1,
    Deferred,
}
