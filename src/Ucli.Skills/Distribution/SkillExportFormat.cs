namespace MackySoft.Ucli.Skills.Distribution;

/// <summary> Defines supported SKILL export output formats. </summary>
public enum SkillExportFormat
{
    /// <summary> Export materialized SKILL directories under the output root. </summary>
    Directory = 0,

    /// <summary> Export materialized SKILL directories into one deterministic zip file. </summary>
    Zip = 1,
}
