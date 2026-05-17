namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Converts Unity execution mode and target values to ready payload literals. </summary>
internal static class ReadyExecutionModeCodec
{
    public const string Auto = AssuranceExecutionModeCodec.Auto;

    public const string Daemon = AssuranceExecutionModeCodec.Daemon;

    public const string Oneshot = AssuranceExecutionModeCodec.Oneshot;

    public const string NotApplicable = AssuranceExecutionModeCodec.NotApplicable;

    /// <summary> Converts one requested mode to its public literal. </summary>
    public static string ToRequestedModeValue (UnityExecutionMode mode)
    {
        return AssuranceExecutionModeCodec.ToRequestedModeValue(mode);
    }

    /// <summary> Converts one resolved target to its public mode literal. </summary>
    public static string ToResolvedModeValue (UnityExecutionTarget target)
    {
        return AssuranceExecutionModeCodec.ToResolvedModeValue(target);
    }

    /// <summary> Converts one resolved target to its session-kind literal. </summary>
    public static string ToSessionKindValue (UnityExecutionTarget target)
    {
        return AssuranceExecutionModeCodec.ToSessionKindValue(target);
    }
}
