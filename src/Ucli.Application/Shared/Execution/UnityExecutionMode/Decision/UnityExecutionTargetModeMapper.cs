namespace MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

/// <summary> Maps a resolved Unity execution target to the matching non-auto execution mode. </summary>
internal static class UnityExecutionTargetModeMapper
{
    /// <summary> Returns the explicit mode that preserves the resolved execution target. </summary>
    /// <param name="target"> The resolved Unity execution target. </param>
    /// <returns> The matching daemon or oneshot execution mode. </returns>
    public static UnityExecutionMode ToExplicitMode (UnityExecutionTarget target)
    {
        return target switch
        {
            UnityExecutionTarget.Daemon => UnityExecutionMode.Daemon,
            UnityExecutionTarget.Oneshot => UnityExecutionMode.Oneshot,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported execution target."),
        };
    }
}
