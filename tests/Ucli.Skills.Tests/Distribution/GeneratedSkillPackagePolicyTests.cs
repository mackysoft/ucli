namespace MackySoft.Ucli.Skills.Tests.Distribution;

public sealed class GeneratedSkillPackagePolicyTests
{
    private const UnixFileMode ExecutableFileModes =
        UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    private static readonly string[] ForbiddenCanonicalSchemaTerms =
    [
        "argsSchema",
        "resultSchema",
    ];

    private static readonly string[] ForbiddenDangerousWorkflowExamples =
    [
        "ucli call --allowDangerous",
        "ucli plan --allowDangerous",
        "ucli validate --allowDangerous",
        "arbitrary C#",
        "execute C#",
        "run C#",
        "arbitrary shell",
        "execute shell",
        "run shell",
        "edit Unity YAML directly",
        "modify Unity YAML directly",
        "write Unity YAML directly",
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void GeneratedSkills_KeepInstructionGuardrails ()
    {
        var skillsRoot = SkillTestData.GetGeneratedSkillsRoot();

        foreach (var skillName in SkillTestData.ExpectedSkillNames)
        {
            var skillDirectory = Path.Combine(skillsRoot, skillName);
            var skillText = File.ReadAllText(Path.Combine(skillDirectory, "SKILL.md"));

            Assert.InRange(CountLogicalLines(skillText), 1, 499);
            Assert.Contains("ucli ops describe <opName>", skillText, StringComparison.Ordinal);
            Assert.Contains("read -> describe -> build request -> validate -> plan -> call -> verify", skillText, StringComparison.Ordinal);
            Assert.Contains("fixed sleep", skillText, StringComparison.Ordinal);
            Assert.Contains("IPC_TIMEOUT", skillText, StringComparison.Ordinal);
            Assert.Contains("payload.opResults[].applied", skillText, StringComparison.Ordinal);
            Assert.Contains("changed", skillText, StringComparison.Ordinal);
            Assert.Contains("touched", skillText, StringComparison.Ordinal);
            Assert.Contains("readPostcondition", skillText, StringComparison.Ordinal);
            Assert.Contains("--allowDangerous", skillText, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(skillDirectory, "scripts")), skillName);

            foreach (var forbiddenTerm in ForbiddenCanonicalSchemaTerms)
            {
                Assert.DoesNotContain(forbiddenTerm, skillText, StringComparison.OrdinalIgnoreCase);
            }

            foreach (var forbiddenTerm in ForbiddenDangerousWorkflowExamples)
            {
                Assert.DoesNotContain(forbiddenTerm, skillText, StringComparison.OrdinalIgnoreCase);
            }

            Assert.False(ContainsOperationCatalogTableCopy(skillText), skillName);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GeneratedReferences_AreBoundedAndNonCanonical ()
    {
        var skillsRoot = SkillTestData.GetGeneratedSkillsRoot();

        foreach (var referencePath in Directory.EnumerateFiles(skillsRoot, "*.md", SearchOption.AllDirectories)
            .Where(static path => path.Split(Path.DirectorySeparatorChar).Contains("references", StringComparer.Ordinal)))
        {
            var text = File.ReadAllText(referencePath);

            Assert.InRange(CountLogicalLines(text), 1, 999);

            foreach (var forbiddenTerm in ForbiddenCanonicalSchemaTerms)
            {
                Assert.DoesNotContain(forbiddenTerm, text, StringComparison.OrdinalIgnoreCase);
            }

            foreach (var forbiddenTerm in ForbiddenDangerousWorkflowExamples)
            {
                Assert.DoesNotContain(forbiddenTerm, text, StringComparison.OrdinalIgnoreCase);
            }

            Assert.False(ContainsOperationCatalogTableCopy(text), referencePath);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GeneratedSkills_DoNotIncludeExecutableFiles ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(SkillTestData.GetGeneratedSkillsRoot(), "*", SearchOption.AllDirectories))
        {
            Assert.Equal((UnixFileMode)0, File.GetUnixFileMode(path) & ExecutableFileModes);
        }
    }

    private static int CountLogicalLines (string text)
    {
        var lineCount = 0;
        using var reader = new StringReader(text);

        while (reader.ReadLine() is not null)
        {
            lineCount++;
        }

        return lineCount;
    }

    private static bool ContainsOperationCatalogTableCopy (string text)
    {
        var operationTableRows = 0;
        using var reader = new StringReader(text);

        while (reader.ReadLine() is { } line)
        {
            if (line.TrimStart().StartsWith("|", StringComparison.Ordinal)
                && line.Contains("ucli.", StringComparison.OrdinalIgnoreCase))
            {
                operationTableRows++;
            }
        }

        return operationTableRows >= 3;
    }
}
