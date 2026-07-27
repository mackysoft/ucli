namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Represents one file diff emitted by a SKILL lifecycle command. </summary>
internal sealed record SkillsOperationFileDiffCommandPayload (
    string RelativePath,
    UcliSkillDiffChangeKind ChangeKind,
    string? BeforeContent,
    string? AfterContent);
