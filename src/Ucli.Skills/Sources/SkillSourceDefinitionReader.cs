using System.Text.Json;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Sources;

/// <summary> Reads and validates source SKILL definitions from <c>SkillDefinitions</c>. </summary>
public sealed class SkillSourceDefinitionReader
{
    private static readonly string[] ExpectedJsonProperties =
    [
        "schemaVersion",
        "skillName",
        "displayName",
        "description",
        "references",
    ];

    /// <summary> Reads all source definitions under a definitions root. </summary>
    /// <param name="definitionsRoot"> The <c>SkillDefinitions</c> directory path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The source definitions or validation failure. </returns>
    public async ValueTask<SkillOperationResult<IReadOnlyList<SkillSourceDefinition>>> ReadAllAsync (
        string definitionsRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionsRoot);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(definitionsRoot))
        {
            return Failure($"SkillDefinitions directory does not exist: {definitionsRoot}");
        }

        var definitions = new List<SkillSourceDefinition>();
        foreach (var skillDirectory in Directory.GetDirectories(definitionsRoot).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await ReadOneAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return Failure(result.Failure!.Message);
            }

            definitions.Add(result.Value!);
        }

        return SkillOperationResult<IReadOnlyList<SkillSourceDefinition>>.Success(definitions);
    }

    /// <summary> Reads one source definition directory. </summary>
    /// <param name="skillDirectory"> The source skill directory path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The source definition or validation failure. </returns>
    public async ValueTask<SkillOperationResult<SkillSourceDefinition>> ReadOneAsync (
        string skillDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var skillName = Path.GetFileName(Path.GetFullPath(skillDirectory));
        SkillOperationResult<SkillSourceMetadata> metadataResult;
        try
        {
            metadataResult = await ReadMetadataAsync(Path.Combine(skillDirectory, "skill.json"), skillName, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return SkillOperationResult<SkillSourceDefinition>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                $"skill.json is invalid for '{skillName}'.");
        }

        if (!metadataResult.IsSuccess)
        {
            return SkillOperationResult<SkillSourceDefinition>.FailureResult(SkillFailureCodes.SourceInvalid, metadataResult.Failure!.Message);
        }

        var templatePath = Path.Combine(skillDirectory, "SKILL.md.template");
        if (!File.Exists(templatePath))
        {
            return SkillOperationResult<SkillSourceDefinition>.FailureResult(SkillFailureCodes.SourceInvalid, $"SKILL.md.template is missing for '{skillName}'.");
        }

        var skillTemplate = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(templatePath, cancellationToken).ConfigureAwait(false));
        if (skillTemplate.TrimStart().StartsWith("---", StringComparison.Ordinal))
        {
            return SkillOperationResult<SkillSourceDefinition>.FailureResult(SkillFailureCodes.SourceInvalid, $"SKILL.md.template must not contain frontmatter: {skillName}");
        }

        var references = new List<SkillSourceReference>();
        foreach (var reference in metadataResult.Value!.References)
        {
            var referenceTemplatePath = Path.Combine(skillDirectory, "references", reference + ".template");
            if (!File.Exists(referenceTemplatePath))
            {
                return SkillOperationResult<SkillSourceDefinition>.FailureResult(
                    SkillFailureCodes.SourceInvalid,
                    $"Reference template '{reference}.template' is missing for '{skillName}'.");
            }

            var referenceTemplate = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(referenceTemplatePath, cancellationToken).ConfigureAwait(false));
            references.Add(new SkillSourceReference(reference, referenceTemplate));
        }

        return SkillOperationResult<SkillSourceDefinition>.Success(new SkillSourceDefinition(metadataResult.Value, skillTemplate, references));
    }

    private static async ValueTask<SkillOperationResult<SkillSourceMetadata>> ReadMetadataAsync (
        string metadataPath,
        string expectedSkillName,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(metadataPath))
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(SkillFailureCodes.SourceInvalid, $"skill.json is missing for '{expectedSkillName}'.");
        }

        using var stream = File.OpenRead(metadataPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(SkillFailureCodes.SourceInvalid, "skill.json root must be an object.");
        }

        var propertyNames = root.EnumerateObject().Select(static property => property.Name).ToArray();
        if (!ExpectedJsonProperties.SequenceEqual(propertyNames))
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                "skill.json must contain only schemaVersion, skillName, displayName, description, and references in canonical order.");
        }

        var schemaVersion = root.GetProperty("schemaVersion").GetInt32();
        if (schemaVersion != SkillSourceMetadata.CurrentSchemaVersion)
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(SkillFailureCodes.SourceInvalid, $"Unsupported skill.json schemaVersion: {schemaVersion}");
        }

        var skillName = root.GetProperty("skillName").GetString() ?? string.Empty;
        if (!string.Equals(skillName, expectedSkillName, StringComparison.Ordinal))
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                $"skill.json skillName '{skillName}' must match directory name '{expectedSkillName}'.");
        }

        var displayName = root.GetProperty("displayName").GetString() ?? string.Empty;
        var description = root.GetProperty("description").GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(description) || description.Length > 1024)
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(SkillFailureCodes.SourceInvalid, $"skill.json displayName and description are invalid for '{skillName}'.");
        }

        var references = root.GetProperty("references").EnumerateArray().Select(static element => element.GetString() ?? string.Empty).ToArray();
        if (references.Length == 0 || references.Distinct(StringComparer.Ordinal).Count() != references.Length)
        {
            return SkillOperationResult<SkillSourceMetadata>.FailureResult(SkillFailureCodes.SourceInvalid, $"skill.json references are invalid for '{skillName}'.");
        }

        foreach (var reference in references)
        {
            if (string.IsNullOrWhiteSpace(reference)
                || !reference.EndsWith(".md", StringComparison.Ordinal)
                || reference.Contains('/', StringComparison.Ordinal)
                || reference.Contains('\\', StringComparison.Ordinal)
                || reference is "." or "..")
            {
                return SkillOperationResult<SkillSourceMetadata>.FailureResult(SkillFailureCodes.SourceInvalid, $"Reference path is unsafe for '{skillName}': {reference}");
            }
        }

        return SkillOperationResult<SkillSourceMetadata>.Success(new SkillSourceMetadata(schemaVersion, skillName, displayName, description, references));
    }

    private static SkillOperationResult<IReadOnlyList<SkillSourceDefinition>> Failure (string message)
    {
        return SkillOperationResult<IReadOnlyList<SkillSourceDefinition>>.FailureResult(SkillFailureCodes.SourceInvalid, message);
    }
}
