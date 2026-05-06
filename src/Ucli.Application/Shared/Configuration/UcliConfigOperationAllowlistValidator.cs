using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Validates and normalizes operation allowlist patterns from config values. </summary>
internal static class UcliConfigOperationAllowlistValidator
{
    /// <summary> Builds normalized operation allowlist patterns while adding diagnostics for invalid entries. </summary>
    public static List<string> BuildNormalizedPatterns (
        IReadOnlyList<string> source,
        string sourcePath,
        string emptyPatternCode,
        string invalidRegexPatternCode,
        List<UcliConfigDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateArguments(sourcePath, emptyPatternCode, invalidRegexPatternCode, diagnostics);

        var normalizedPatterns = new List<string>(source.Count);
        for (var i = 0; i < source.Count; i++)
        {
            var propertyPath = $"{UcliConfigJsonPropertyNames.OperationAllowlist}[{i}]";
            if (!StringValueNormalizer.TryTrimToNonEmpty(source[i], out var pattern))
            {
                if (!AddEmptyPatternDiagnostic(diagnostics, emptyPatternCode, propertyPath, sourcePath))
                {
                    break;
                }

                continue;
            }

            if (!TryValidateRegexPattern(
                diagnostics,
                invalidRegexPatternCode,
                propertyPath,
                sourcePath,
                pattern,
                out var canContinue))
            {
                if (!canContinue)
                {
                    break;
                }

                continue;
            }

            normalizedPatterns.Add(pattern);
        }

        return normalizedPatterns;
    }

    /// <summary> Adds save-time diagnostics for operation allowlist patterns. </summary>
    public static void AddSaveDiagnostics (
        IReadOnlyList<string> source,
        string sourcePath,
        string emptyPatternCode,
        string invalidRegexPatternCode,
        List<UcliConfigDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateArguments(sourcePath, emptyPatternCode, invalidRegexPatternCode, diagnostics);

        for (var i = 0; i < source.Count; i++)
        {
            var propertyPath = $"{UcliConfigJsonPropertyNames.OperationAllowlist}[{i}]";
            var pattern = source[i];
            if (string.IsNullOrWhiteSpace(pattern))
            {
                if (!AddEmptyPatternDiagnostic(diagnostics, emptyPatternCode, propertyPath, sourcePath))
                {
                    break;
                }

                continue;
            }

            if (!TryValidateRegexPattern(
                diagnostics,
                invalidRegexPatternCode,
                propertyPath,
                sourcePath,
                pattern,
                out var canContinue))
            {
                if (!canContinue)
                {
                    break;
                }

                continue;
            }
        }
    }

    private static UcliConfigDiagnostic CreateDiagnostic (
        string code,
        string propertyPath,
        string sourcePath,
        string message)
    {
        return UcliConfigDiagnostic.Create(code, propertyPath, sourcePath, message);
    }

    private static bool AddDiagnostic (
        List<UcliConfigDiagnostic> diagnostics,
        UcliConfigDiagnostic diagnostic)
    {
        return UcliConfigDiagnosticList.Add(diagnostics, diagnostic);
    }

    private static bool AddEmptyPatternDiagnostic (
        List<UcliConfigDiagnostic> diagnostics,
        string emptyPatternCode,
        string propertyPath,
        string sourcePath)
    {
        return AddDiagnostic(diagnostics, CreateDiagnostic(
            emptyPatternCode,
            propertyPath,
            sourcePath,
            "Config operationAllowlist contains an empty pattern."));
    }

    private static bool TryValidateRegexPattern (
        List<UcliConfigDiagnostic> diagnostics,
        string invalidRegexPatternCode,
        string propertyPath,
        string sourcePath,
        string pattern,
        out bool canContinue)
    {
        if (RegexPatternUtilities.TryValidatePattern(pattern, out var patternErrorMessage))
        {
            canContinue = true;
            return true;
        }

        var displayPattern = UcliConfigDiagnostic.FormatFragment(pattern);
        var displayPatternErrorMessage = UcliConfigDiagnostic.FormatFragment(patternErrorMessage);
        canContinue = AddDiagnostic(diagnostics, CreateDiagnostic(
            invalidRegexPatternCode,
            propertyPath,
            sourcePath,
            $"Config operationAllowlist contains an invalid regex pattern: {displayPattern}. {displayPatternErrorMessage}"));
        return false;
    }

    private static void ValidateArguments (
        string sourcePath,
        string emptyPatternCode,
        string invalidRegexPatternCode,
        List<UcliConfigDiagnostic> diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(emptyPatternCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(invalidRegexPatternCode);
        ArgumentNullException.ThrowIfNull(diagnostics);
    }
}
