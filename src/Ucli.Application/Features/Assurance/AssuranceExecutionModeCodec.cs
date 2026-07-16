namespace MackySoft.Ucli.Application.Features.Assurance;

/// <summary> Maps internal Unity execution decisions to public assurance contract values. </summary>
internal static class AssuranceExecutionModeCodec
{
    /// <summary> Maps one requested application mode to its public assurance value. </summary>
    public static AssuranceRequestedExecutionMode ToRequestedMode (UnityExecutionMode mode)
    {
        return mode switch
        {
            UnityExecutionMode.Auto => AssuranceRequestedExecutionMode.Auto,
            UnityExecutionMode.Daemon => AssuranceRequestedExecutionMode.Daemon,
            UnityExecutionMode.Oneshot => AssuranceRequestedExecutionMode.Oneshot,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported execution mode."),
        };
    }

    /// <summary> Maps one resolved application target to its public assurance value. </summary>
    public static AssuranceResolvedExecutionMode ToResolvedMode (UnityExecutionTarget target)
    {
        return target switch
        {
            UnityExecutionTarget.Daemon => AssuranceResolvedExecutionMode.Daemon,
            UnityExecutionTarget.Oneshot => AssuranceResolvedExecutionMode.Oneshot,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported execution target."),
        };
    }

    /// <summary> Converts one resolved target to its assurance session kind. </summary>
    public static AssuranceSessionKind ToSessionKind (UnityExecutionTarget target)
    {
        return target switch
        {
            UnityExecutionTarget.Daemon => AssuranceSessionKind.Daemon,
            UnityExecutionTarget.Oneshot => AssuranceSessionKind.TransientProbe,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported execution target."),
        };
    }
}
