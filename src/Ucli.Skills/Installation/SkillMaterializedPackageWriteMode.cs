namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Defines the target existence precondition for a materialized package write. </summary>
public enum SkillMaterializedPackageWriteMode
{
    /// <summary> The target directory must be absent at commit time. </summary>
    CreateNew,

    /// <summary> The target directory must be present and eligible for replacement at commit time. </summary>
    ReplaceExisting,
}
