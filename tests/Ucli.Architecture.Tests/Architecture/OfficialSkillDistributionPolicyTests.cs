using System.Text.RegularExpressions;

namespace MackySoft.Ucli.Architecture.Tests.Architecture;

public sealed class OfficialSkillDistributionPolicyTests
{
    private static readonly Regex ErrorsCommandPattern = new(
        @"\bucli\s+errors\b|\berrors\s+(describe|list|explain)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ScriptProfileSideEffectGuidanceLinePattern = new(
        @"^\d+\. For C# script changes, run `ucli compile` or `ucli verify --profile built-in:script --from <result\.json>`\. (?:Omit `--profile` only when `built-in:default` project-level verification is intended, because it can trigger compile / domain reload\.|Use `built-in:default` only when compile / domain reload side effects are intended\.)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] SkillPolicyRelativeRoots =
    [
        "skills/definitions",
        "skills/generated",
    ];

    private static readonly string[] PlanApplyWorkflowRelativePaths =
    [
        "skills/definitions/ucli-plan-apply/SKILL.md.template",
        "skills/definitions/ucli-plan-apply/references/request-workflow.md.template",
        "skills/generated/ucli-plan-apply/SKILL.md",
        "skills/generated/ucli-plan-apply/references/request-workflow.md",
    ];

    private static readonly string[] PlanApplyMetadataRelativePaths =
    [
        "skills/definitions/ucli-plan-apply/skill.json",
        "skills/generated/ucli-plan-apply/agent-skill.json",
        "skills/generated/ucli-plan-apply/agents/openai.yaml",
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

    private const string CurrentPlanApplyWorkflowSummary = "read -> ready -> describe -> build request -> validate -> plan -> call --withPlan -> verify";
    private const string LegacyPlanApplyWorkflowSummary = "read -> describe -> build request -> validate -> plan -> call -> verify";

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

    private static readonly string[] SensitivePolicyWarningLines =
    [
        "- Do not use fixed sleep while waiting for readiness. Let uCLI lifecycle and timeout results drive the next step.",
        "- Do not use fixed sleep before verification. Re-read state or inspect lifecycle/log evidence instead.",
        "- Do not use fixed sleep as a readiness strategy. Use uCLI lifecycle, status, logs, and command results.",
        "- Do not use fixed sleep to wait out compile, reload, or daemon readiness. Use lifecycle-aware uCLI commands and bounded timeouts.",
        "- Do not use log scraping as a pass/fail gate. Use claim packets and bounded log commands only when a code or claim requires evidence.",
        "- Do not use log scraping as proof of success or failure; prefer claim packets and machine-readable command output.",
        "- Do not use `--all` in normal reasoning loops. Page with `--limit` and `--after`.",
        "- Do not use arbitrary C# execution, arbitrary shell execution, or Unity YAML direct edits as normal shortcuts.",
        "- Do not include `--allowDangerous` in normal workflows. Dangerous operation opt-in requires explicit user intent.",
        "- Do not include `--allowDangerous` in normal workflows. Only discuss dangerous opt-in when the user explicitly asks for that path.",
        "- Do not include `--allowDangerous` in normal verification workflows.",
        "- Do not include `--allowDangerous` in normal troubleshooting workflows.",
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
        foreach (var relativePath in EnumerateRootSkillMarkdownRelativePaths())
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
        foreach (var relativePath in PlanApplyWorkflowRelativePaths)
        {
            var text = ReadRepositoryText(relativePath);
            foreach (var fragment in NormalMutationWorkflowFragments.Concat(ScriptVerificationFragments))
            {
                Assert.Contains(fragment, text, StringComparison.Ordinal);
            }

            Assert.True(
                ContainsLineMatching(text, ScriptProfileSideEffectGuidanceLinePattern),
                $"{relativePath} must keep built-in:default tied to compile / domain reload side effects.");
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Plan_apply_skill_metadata_uses_current_assurance_workflow_summary ()
    {
        foreach (var relativePath in PlanApplyMetadataRelativePaths)
        {
            var text = ReadRepositoryText(relativePath);

            Assert.Contains(CurrentPlanApplyWorkflowSummary, text, StringComparison.Ordinal);
            Assert.DoesNotContain(LegacyPlanApplyWorkflowSummary, text, StringComparison.Ordinal);
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
            Assert.True(
                ContainsLineMatching(text, ScriptProfileSideEffectGuidanceLinePattern),
                "Verify skill must keep built-in:default tied to compile / domain reload side effects.");
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

    [Fact]
    [Trait("Size", "Small")]
    public void Sensitive_policy_warning_classifier_accepts_current_explicit_warning_lines ()
    {
        foreach (var line in SensitivePolicyWarningLines)
        {
            Assert.True(IsAllowedSensitivePolicyLine(line), line);
        }
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("- Do not block arbitrary shell execution in normal workflows.")]
    [InlineData("- This is not proof that --allowDangerous is unsafe.")]
    [InlineData("- Use --all after the first query.")]
    [InlineData("- Do not use log scraping as a pass/fail gate. Use --allowDangerous instead.")]
    [InlineData("- Do not use fixed sleep while waiting for readiness. Let uCLI lifecycle and timeout results drive the next step. Then use fixed sleep anyway.")]
    [InlineData("- Do not include `--allowDangerous` in normal workflows. Then use `--allowDangerous` anyway.")]
    public void Sensitive_policy_warning_classifier_rejects_unsafe_lines_with_warning_words (string line)
    {
        Assert.False(IsAllowedSensitivePolicyLine(line));
    }

    private static IEnumerable<string> EnumerateSkillFiles ()
    {
        foreach (var relativeRoot in SkillPolicyRelativeRoots)
        {
            var root = ArchitectureTestRepository.ToRegularDirectoryFullPath(relativeRoot);
            foreach (var file in ArchitectureTestRepository.EnumerateRegularFilesUnderDirectory(root))
            {
                yield return file;
            }
        }
    }

    private static IEnumerable<string> EnumerateSkillMarkdownFiles ()
    {
        return EnumerateSkillFiles()
            .Where(static file => file.EndsWith(".md", StringComparison.Ordinal)
                                  || file.EndsWith(".md.template", StringComparison.Ordinal));
    }

    private static IEnumerable<string> EnumerateRootSkillMarkdownRelativePaths ()
    {
        return EnumerateSkillMarkdownFiles()
            .Where(static file =>
            {
                var fileName = Path.GetFileName(file);
                return fileName.Equals("SKILL.md", StringComparison.Ordinal)
                    || fileName.Equals("SKILL.md.template", StringComparison.Ordinal);
            })
            .Select(ArchitectureTestRepository.NormalizeRepositoryRelativePath);
    }

    private static IEnumerable<string> FindRegexMatches (string file, Regex pattern)
    {
        var relativePath = ArchitectureTestRepository.NormalizeRepositoryRelativePath(file);
        var lines = ReadRepositoryText(relativePath).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = TrimLineEnding(lines[i]);
            if (pattern.IsMatch(line))
            {
                yield return $"{relativePath}:{i + 1}: {line}";
            }
        }
    }

    private static IEnumerable<string> FindSensitivePolicyViolations (string file)
    {
        var relativePath = ArchitectureTestRepository.NormalizeRepositoryRelativePath(file);
        var lines = ReadRepositoryText(relativePath).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = TrimLineEnding(lines[i]);
            if (!SensitivePolicyFragments.Any(fragment => line.Contains(fragment, StringComparison.Ordinal)))
            {
                continue;
            }

            if (IsAllowedSensitivePolicyLine(line))
            {
                continue;
            }

            yield return $"{relativePath}:{i + 1}: {line}";
        }
    }

    private static bool IsAllowedSensitivePolicyLine (string line)
    {
        return SensitivePolicyWarningLines.Contains(line, StringComparer.Ordinal);
    }

    private static bool ContainsLineMatching (string text, Regex pattern)
    {
        return text.Split('\n')
            .Select(TrimLineEnding)
            .Any(pattern.IsMatch);
    }

    private static string TrimLineEnding (string line)
    {
        return line.TrimEnd('\r');
    }

    private static string ReadRepositoryText (string relativePath)
    {
        return File.ReadAllText(ArchitectureTestRepository.ToRegularFileFullPath(relativePath));
    }
}
