using System.Text.RegularExpressions;

namespace MackySoft.Ucli.Architecture.Tests.Architecture;

public sealed class OfficialSkillDistributionPolicyTests
{
    private static readonly Regex ErrorsCommandPattern = new(
        @"\bucli\s+errors\b|\berrors\s+(describe|list|explain)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] RootSkillRelativePaths =
    [
        "skills/definitions/ucli-plan-apply/SKILL.md.template",
        "skills/definitions/ucli-read-project/SKILL.md.template",
        "skills/definitions/ucli-troubleshoot/SKILL.md.template",
        "skills/definitions/ucli-verify-changes/SKILL.md.template",
        "skills/generated/ucli-plan-apply/SKILL.md",
        "skills/generated/ucli-read-project/SKILL.md",
        "skills/generated/ucli-troubleshoot/SKILL.md",
        "skills/generated/ucli-verify-changes/SKILL.md",
    ];

    private static readonly string[] SkillPolicyRelativeRoots =
    [
        "skills/definitions",
        "skills/generated",
    ];

    private static readonly string[] NormalMutationWorkflowFragments =
    [
        "ucli ready --for mutation",
        "ucli ops describe <opName>",
        "ucli validate",
        "ucli plan",
        "ucli call --withPlan",
        "ucli verify --profile built-in:mutation --from <result.json>",
    ];

    private static readonly string[] ScriptVerificationFragments =
    [
        "ucli compile",
        "ucli verify --profile built-in:script",
        "built-in:default",
        "compile / domain reload",
    ];

    private static readonly string[] SensitivePolicyFragments =
    [
        "--all",
        "--allowDangerous",
        "fixed sleep",
        "log scraping",
        "arbitrary C#",
        "arbitrary shell",
        "Unity YAML",
        "任意 C#",
        "任意 shell",
        "YAML 直編集",
    ];

    private static readonly string[] WarningLineFragments =
    [
        "Do not",
        "Only discuss",
        "requires explicit user intent",
        "not proof",
        "do not",
        "禁止",
        "含めない",
        "推奨しない",
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void Official_skill_sources_do_not_reference_errors_command ()
    {
        var violations = EnumerateSkillFiles()
            .SelectMany(static file => FindRegexMatches(file, ErrorsCommandPattern))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Official_skill_root_workflows_reference_codes_and_ops_catalogs ()
    {
        foreach (var relativePath in RootSkillRelativePaths)
        {
            var text = ReadRepositoryText(relativePath);

            Assert.Contains("ucli ops describe <opName>", text, StringComparison.Ordinal);
            Assert.Contains("ucli codes describe <CODE>", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Plan_apply_skill_references_current_assurance_mutation_workflow ()
    {
        var sourceTemplate = ReadRepositoryText("skills/definitions/ucli-plan-apply/SKILL.md.template");
        var generatedSkill = ReadRepositoryText("skills/generated/ucli-plan-apply/SKILL.md");

        foreach (var fragment in NormalMutationWorkflowFragments.Concat(ScriptVerificationFragments))
        {
            Assert.Contains(fragment, sourceTemplate, StringComparison.Ordinal);
            Assert.Contains(fragment, generatedSkill, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Verify_skill_documents_profile_side_effects_and_probe_only_limit ()
    {
        var sourceTemplate = ReadRepositoryText("skills/definitions/ucli-verify-changes/SKILL.md.template");
        var generatedSkill = ReadRepositoryText("skills/generated/ucli-verify-changes/SKILL.md");

        foreach (var text in new[] { sourceTemplate, generatedSkill })
        {
            Assert.Contains("ucli verify --profile built-in:mutation --from <result.json>", text, StringComparison.Ordinal);
            Assert.Contains("ucli compile", text, StringComparison.Ordinal);
            Assert.Contains("ucli verify --profile built-in:script", text, StringComparison.Ordinal);
            Assert.Contains("built-in:default", text, StringComparison.Ordinal);
            Assert.Contains("compile / domain reload", text, StringComparison.Ordinal);
            Assert.Contains("ready --mode auto", text, StringComparison.Ordinal);
            Assert.Contains("probeOnly", text, StringComparison.Ordinal);
            Assert.Contains("reusable session", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Official_skill_policy_sensitive_terms_are_warning_only ()
    {
        var violations = EnumerateSkillMarkdownFiles()
            .SelectMany(static file => FindSensitivePolicyViolations(file))
            .ToArray();

        Assert.Empty(violations);
    }

    private static IEnumerable<string> EnumerateSkillFiles ()
    {
        foreach (var relativeRoot in SkillPolicyRelativeRoots)
        {
            var root = ArchitectureTestRepository.ToFullPath(relativeRoot);
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                if (!ArchitectureTestRepository.IsReparsePoint(file))
                {
                    yield return file;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateSkillMarkdownFiles ()
    {
        return EnumerateSkillFiles()
            .Where(static file => file.EndsWith(".md", StringComparison.Ordinal)
                                  || file.EndsWith(".md.template", StringComparison.Ordinal));
    }

    private static IEnumerable<string> FindRegexMatches (string file, Regex pattern)
    {
        var relativePath = ArchitectureTestRepository.NormalizeRepositoryRelativePath(file);
        var lines = File.ReadAllText(file).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (pattern.IsMatch(lines[i]))
            {
                yield return $"{relativePath}:{i + 1}: {lines[i]}";
            }
        }
    }

    private static IEnumerable<string> FindSensitivePolicyViolations (string file)
    {
        var relativePath = ArchitectureTestRepository.NormalizeRepositoryRelativePath(file);
        var lines = File.ReadAllText(file).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!SensitivePolicyFragments.Any(fragment => line.Contains(fragment, StringComparison.Ordinal)))
            {
                continue;
            }

            if (WarningLineFragments.Any(fragment => line.Contains(fragment, StringComparison.Ordinal)))
            {
                continue;
            }

            yield return $"{relativePath}:{i + 1}: {line}";
        }
    }

    private static string ReadRepositoryText (string relativePath)
    {
        return File.ReadAllText(ArchitectureTestRepository.ToRegularFileFullPath(relativePath));
    }
}
