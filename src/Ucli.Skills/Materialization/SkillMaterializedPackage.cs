using MackySoft.Ucli.Skills.Hosts;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Materialization;

/// <summary> Represents a host-materialized SKILL package. </summary>
/// <param name="SkillName"> The skill name. </param>
/// <param name="Host"> The host. </param>
/// <param name="Files"> The materialized package files. </param>
public sealed record SkillMaterializedPackage (
    string SkillName,
    SkillHostKind Host,
    IReadOnlyList<SkillPackageFile> Files);
