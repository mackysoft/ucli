namespace MackySoft.Ucli.Skills.Hosts;

/// <summary> Defines supported SKILL host adapters. </summary>
public enum SkillHostKind
{
    /// <summary> Claude Code project skills. </summary>
    Claude = 0,

    /// <summary> GitHub Copilot CLI project skills. </summary>
    Copilot = 1,

    /// <summary> OpenAI / Codex project skills. </summary>
    OpenAi = 2,
}
