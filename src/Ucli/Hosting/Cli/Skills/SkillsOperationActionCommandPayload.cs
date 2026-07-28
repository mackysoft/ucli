namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Represents one action emitted by a SKILL lifecycle command. </summary>
internal sealed record SkillsOperationActionCommandPayload<TAction> (
    string SkillName,
    TAction Action,
    string TargetRoot,
    UcliSkillBlockedReason? BlockedReason,
    IReadOnlyList<SkillsOperationDiffCommandPayload> Diffs)
    where TAction : struct, Enum;
