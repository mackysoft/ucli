using MackySoft.Ucli.Application.Features.Programs.Parsing;

namespace MackySoft.Ucli.Application.Features.Programs.Persistence;

/// <summary> Validates the fixed definition, effective configuration, and durable Program Run Step binding. </summary>
internal static class ProgramRunDefinitionBinding
{
    public static void Validate (ProgramRunRecord run, ProgramDefinitionSnapshotFixedDefinition fixedDefinition)
    {
        ArgumentNullException.ThrowIfNull(run);
        Validate(run.Steps, run.FixedContext, fixedDefinition);
    }

    public static void Validate (
        IReadOnlyList<ProgramRunStepRecord> steps,
        ProgramRunFixedContext fixedContext,
        ProgramDefinitionSnapshotFixedDefinition fixedDefinition)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(fixedContext);
        ArgumentNullException.ThrowIfNull(fixedDefinition);
        if (steps.Count != fixedDefinition.Steps.Count)
        {
            throw new ArgumentException("Program Run Steps must match the fixed Program definition count.");
        }

        for (var index = 0; index < steps.Count; index++)
        {
            var command = GetCommand(fixedDefinition.Steps[index]);
            if (steps[index].Command != command
                || steps[index].TimeoutMilliseconds != ResolveTimeoutMilliseconds(fixedDefinition.Steps[index], command, fixedContext))
            {
                throw new ArgumentException("Program Run Steps must preserve fixed Program order, command, and effective timeout.");
            }
        }
    }

    private static int ResolveTimeoutMilliseconds (ProgramStep definitionStep, string command, ProgramRunFixedContext fixedContext)
    {
        if (definitionStep.TimeoutMilliseconds.HasValue)
        {
            return definitionStep.TimeoutMilliseconds.Value;
        }
        if (!fixedContext.Configuration.IpcTimeoutMillisecondsByCommand.TryGetValue(command, out var timeoutMilliseconds)
            || timeoutMilliseconds < 1)
        {
            throw new ArgumentException("Program Run configuration must retain an effective timeout for every implicit Program Step.");
        }
        return timeoutMilliseconds;
    }

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
        _ => throw new ArgumentException("Program Run contains an unsupported Program Step."),
    };
}
