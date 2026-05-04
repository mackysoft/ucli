using System.Text.Json;

namespace MackySoft.Ucli.Skills.Tests.SkillDefinitions;

public sealed class OfficialSkillDefinitionSourceTests
{
    private const UnixFileMode ExecutableFileModes =
        UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    private static readonly string[] ExpectedSkillNames =
    [
        "ucli-plan-apply",
        "ucli-read-project",
        "ucli-troubleshoot",
        "ucli-verify-changes",
    ];

    private static readonly string[] ExpectedJsonProperties =
    [
        "schemaVersion",
        "skillName",
        "displayName",
        "description",
        "references",
    ];

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
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void OfficialSkillDefinitions_HaveExpectedSourceMetadata ()
    {
        var definitionsRoot = GetDefinitionsRoot();
        var directories = Directory.GetDirectories(definitionsRoot)
            .Select(Path.GetFileName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedSkillNames, directories);

        foreach (var skillName in ExpectedSkillNames)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(definitionsRoot, skillName, "skill.json")));
            var root = document.RootElement;

            Assert.Equal(ExpectedJsonProperties, root.EnumerateObject().Select(static property => property.Name).ToArray());
            Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(skillName, root.GetProperty("skillName").GetString());
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("displayName").GetString()));

            var description = root.GetProperty("description").GetString();
            Assert.False(string.IsNullOrWhiteSpace(description));
            Assert.InRange(description.Length, 1, 1024);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OfficialSkillDefinitions_HaveExistingReferenceTemplates ()
    {
        var definitionsRoot = GetDefinitionsRoot();

        foreach (var skillName in ExpectedSkillNames)
        {
            var skillDirectory = Path.Combine(definitionsRoot, skillName);
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(skillDirectory, "skill.json")));
            var references = document.RootElement.GetProperty("references").EnumerateArray().Select(static element => element.GetString()).ToArray();

            Assert.NotEmpty(references);

            foreach (var reference in references)
            {
                Assert.False(string.IsNullOrWhiteSpace(reference));
                Assert.EndsWith(".md", reference, StringComparison.Ordinal);
                Assert.DoesNotContain("/", reference, StringComparison.Ordinal);
                Assert.DoesNotContain("\\", reference, StringComparison.Ordinal);
                Assert.True(File.Exists(Path.Combine(skillDirectory, "references", reference + ".template")), reference);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OfficialSkillDefinitions_KeepInstructionGuardrails ()
    {
        var definitionsRoot = GetDefinitionsRoot();

        foreach (var skillName in ExpectedSkillNames)
        {
            var template = File.ReadAllText(Path.Combine(definitionsRoot, skillName, "SKILL.md.template"));

            Assert.False(template.TrimStart().StartsWith("---", StringComparison.Ordinal));
            Assert.InRange(CountLogicalLines(template), 1, 499);
            Assert.Contains("ucli ops describe <opName>", template, StringComparison.Ordinal);
            Assert.Contains("read -> describe -> build request -> validate -> plan -> call -> verify", template, StringComparison.Ordinal);
            Assert.Contains("fixed sleep", template, StringComparison.Ordinal);
            Assert.Contains("IPC_TIMEOUT", template, StringComparison.Ordinal);
            Assert.Contains("payload.opResults[].applied", template, StringComparison.Ordinal);
            Assert.Contains("changed", template, StringComparison.Ordinal);
            Assert.Contains("touched", template, StringComparison.Ordinal);
            Assert.Contains("readPostcondition", template, StringComparison.Ordinal);
            Assert.Contains("--allowDangerous", template, StringComparison.Ordinal);
            Assert.DoesNotContain("argsSchema", template, StringComparison.Ordinal);
            Assert.DoesNotContain("resultSchema", template, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(definitionsRoot, skillName, "scripts")), skillName);
            Assert.False(Directory.Exists(Path.Combine(definitionsRoot, skillName, "assets")), skillName);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OfficialSkillDefinitions_KeepReferenceTemplatesBoundedAndNonCanonical ()
    {
        var definitionsRoot = GetDefinitionsRoot();

        foreach (var skillName in ExpectedSkillNames)
        {
            var skillDirectory = Path.Combine(definitionsRoot, skillName);
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(skillDirectory, "skill.json")));
            var references = document.RootElement.GetProperty("references").EnumerateArray().Select(static element => element.GetString()).ToArray();

            foreach (var reference in references)
            {
                Assert.False(string.IsNullOrWhiteSpace(reference));

                var referencePath = Path.Combine(skillDirectory, "references", reference + ".template");
                var template = File.ReadAllText(referencePath);

                Assert.InRange(CountLogicalLines(template), 1, 999);

                foreach (var forbiddenTerm in ForbiddenCanonicalSchemaTerms)
                {
                    Assert.DoesNotContain(forbiddenTerm, template, StringComparison.OrdinalIgnoreCase);
                }

                foreach (var forbiddenTerm in ForbiddenDangerousWorkflowExamples)
                {
                    Assert.DoesNotContain(forbiddenTerm, template, StringComparison.OrdinalIgnoreCase);
                }

                Assert.False(ContainsOperationCatalogTableCopy(template), reference);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OfficialSkillDefinitions_DoNotIncludeExecutableFiles ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var definitionsRoot = GetDefinitionsRoot();

        foreach (var path in Directory.EnumerateFiles(definitionsRoot, "*", SearchOption.AllDirectories))
        {
            Assert.Equal((UnixFileMode)0, File.GetUnixFileMode(path) & ExecutableFileModes);
        }
    }

    private static string GetDefinitionsRoot ()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Ucli.Skills", "SkillDefinitions");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate src/Ucli.Skills/SkillDefinitions from the test output directory.");
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
