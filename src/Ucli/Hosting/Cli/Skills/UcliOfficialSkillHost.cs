namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Identifies a host supported by the bundled uCLI SKILL catalog. </summary>
[VocabularyDefinition]
internal enum UcliOfficialSkillHost
{
    /// <summary> Claude Code. </summary>
    [VocabularyText("claude")]
    Claude = 0,

    /// <summary> GitHub Copilot. </summary>
    [VocabularyText("copilot")]
    Copilot = 1,

    /// <summary> OpenAI Codex. </summary>
    [VocabularyText("openai")]
    OpenAi = 2,
}
