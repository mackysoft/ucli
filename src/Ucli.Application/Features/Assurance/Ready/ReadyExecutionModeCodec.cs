namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Converts Unity execution mode and target values to ready payload literals. </summary>
internal static class ReadyExecutionModeCodec
{
    public const string Auto = "auto";

    public const string Daemon = "daemon";

    public const string Oneshot = "oneshot";

    public const string NotApplicable = "notApplicable";

    /// <summary> Converts one requested mode to its public literal. </summary>
    public static string ToRequestedModeValue (UnityExecutionMode mode)
    {
        return mode switch
        {
            UnityExecutionMode.Auto => Auto,
            UnityExecutionMode.Daemon => Daemon,
            UnityExecutionMode.Oneshot => Oneshot,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported execution mode."),
        };
    }

    /// <summary> Converts one resolved target to its public mode literal. </summary>
    public static string ToResolvedModeValue (UnityExecutionTarget target)
    {
        return target switch
        {
            UnityExecutionTarget.Daemon => Daemon,
            UnityExecutionTarget.Oneshot => Oneshot,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported execution target."),
        };
    }

    /// <summary> Converts one resolved target to its session-kind literal. </summary>
    public static string ToSessionKindValue (UnityExecutionTarget target)
    {
        return target switch
        {
            UnityExecutionTarget.Daemon => ReadySessionKindValues.Daemon,
            UnityExecutionTarget.Oneshot => ReadySessionKindValues.TransientProbe,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported execution target."),
        };
    }
}
