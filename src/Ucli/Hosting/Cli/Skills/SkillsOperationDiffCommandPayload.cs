namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Represents one grouped file diff emitted by a SKILL lifecycle command. </summary>
internal sealed record SkillsOperationDiffCommandPayload (
    IReadOnlyList<SkillsOperationFileDiffCommandPayload> Files);
