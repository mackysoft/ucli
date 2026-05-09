using System.Globalization;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;

/// <summary> Classifies daemon startup failures from one Unity batchmode startup log segment. </summary>
internal static class DaemonStartupFailureLogClassifier
{
    /// <summary> Extracts the latest startup log segment from one Unity batchmode log text. </summary>
    /// <param name="logText"> The complete Unity batchmode log text. </param>
    /// <returns> The latest startup log segment, or the original text when no startup marker exists. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="logText" /> is <see langword="null" />. </exception>
    public static string GetLatestStartupLogText (string logText)
    {
        ArgumentNullException.ThrowIfNull(logText);
        const string startupMarker = "COMMAND LINE ARGUMENTS:";
        var markerIndex = logText.LastIndexOf(startupMarker, StringComparison.Ordinal);
        return markerIndex >= 0
            ? logText[markerIndex..]
            : logText;
    }

    /// <summary> Tries to classify one daemon startup failure from one startup log segment. </summary>
    /// <param name="startupLogText"> The latest Unity startup log segment. </param>
    /// <param name="error"> The structured startup failure when classification succeeds. </param>
    /// <returns> <see langword="true" /> when one known startup failure was classified; otherwise, <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="startupLogText" /> is <see langword="null" />. </exception>
    public static bool TryClassify (
        string startupLogText,
        out ExecutionError? error)
    {
        ArgumentNullException.ThrowIfNull(startupLogText);

        if (TryClassifyFailure(startupLogText, out var classification))
        {
            error = ExecutionError.InternalError(classification!.Message);
            return true;
        }

        error = null;
        return false;
    }

    /// <summary> Tries to classify one daemon startup failure from one startup log segment. </summary>
    /// <param name="startupLogText"> The latest Unity startup log segment. </param>
    /// <param name="classification"> The structured startup failure when classification succeeds. </param>
    /// <returns> <see langword="true" /> when one known startup failure was classified; otherwise, <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="startupLogText" /> is <see langword="null" />. </exception>
    public static bool TryClassifyFailure (
        string startupLogText,
        out DaemonStartupFailureClassification? classification)
    {
        ArgumentNullException.ThrowIfNull(startupLogText);

        if (TryGetCompilerErrorSummary(startupLogText, out var compilerErrorSummary, out var compilerDiagnostic))
        {
            classification = new DaemonStartupFailureClassification(
                Reason: DaemonDiagnosisReasonValues.UnityScriptCompilationFailed,
                Message: $"Unity Editor startup is blocked because scripts have compiler errors. {compilerErrorSummary}",
                StartupPhase: DaemonDiagnosisStartupPhaseValues.ScriptCompilation,
                ActionRequired: DaemonDiagnosisActionRequiredValues.FixCompileErrors,
                PrimaryDiagnostic: compilerDiagnostic);
            return true;
        }

        if (TryGetPackageResolutionErrorSummary(startupLogText, out var packageErrorSummary, out var packageDiagnostic))
        {
            classification = new DaemonStartupFailureClassification(
                Reason: DaemonDiagnosisReasonValues.UnityPackageResolutionFailed,
                Message: $"Unity Editor startup is blocked because package resolution failed. {packageErrorSummary}",
                StartupPhase: DaemonDiagnosisStartupPhaseValues.PackageResolution,
                ActionRequired: DaemonDiagnosisActionRequiredValues.ResolvePackages,
                PrimaryDiagnostic: packageDiagnostic);
            return true;
        }

        if (TryGetUserActionRequiredSummary(startupLogText, out var userActionSummary, out var userActionDiagnostic))
        {
            classification = new DaemonStartupFailureClassification(
                Reason: DaemonDiagnosisReasonValues.EditorUserActionRequired,
                Message: $"Unity Editor startup is blocked because Unity requires user action. {userActionSummary}",
                StartupPhase: DaemonDiagnosisStartupPhaseValues.UserAction,
                ActionRequired: DaemonDiagnosisActionRequiredValues.ResolveUnityDialog,
                PrimaryDiagnostic: userActionDiagnostic);
            return true;
        }

        classification = null;
        return false;
    }

    private static bool TryGetCompilerErrorSummary (
        string logText,
        out string summary,
        out DaemonPrimaryDiagnostic? primaryDiagnostic)
    {
        const string compilerErrorsMarker = "Scripts have compiler errors";
        summary = string.Empty;
        primaryDiagnostic = null;

        var lines = logText.Split('\n');
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.Length == 0)
            {
                continue;
            }

            if (trimmedLine.Contains("error CS", StringComparison.OrdinalIgnoreCase))
            {
                summary = $"FirstError={trimmedLine}";
                primaryDiagnostic = TryParseCompilerDiagnostic(trimmedLine, out var diagnostic)
                    ? diagnostic
                    : new DaemonPrimaryDiagnostic(
                        Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler,
                        Code: TryExtractCompilerErrorCode(trimmedLine),
                        File: null,
                        Line: null,
                        Column: null,
                        Message: trimmedLine);
                return true;
            }

            if (trimmedLine.Contains(compilerErrorsMarker, StringComparison.OrdinalIgnoreCase))
            {
                summary = $"Marker={trimmedLine}";
                primaryDiagnostic = new DaemonPrimaryDiagnostic(
                    Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler,
                    Code: null,
                    File: null,
                    Line: null,
                    Column: null,
                    Message: trimmedLine);
                return true;
            }
        }

        return false;
    }

    private static bool TryGetPackageResolutionErrorSummary (
        string logText,
        out string summary,
        out DaemonPrimaryDiagnostic? primaryDiagnostic)
    {
        const string packageFailureMarker = "An error occurred while resolving packages:";
        summary = string.Empty;
        primaryDiagnostic = null;

        var lines = logText.Split('\n');
        var markerFound = false;
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.Length == 0)
            {
                continue;
            }

            if (!markerFound)
            {
                if (trimmedLine.Contains(packageFailureMarker, StringComparison.OrdinalIgnoreCase))
                {
                    markerFound = true;
                    summary = $"Marker={trimmedLine}";
                    primaryDiagnostic = new DaemonPrimaryDiagnostic(
                        Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.PackageResolution,
                        Code: null,
                        File: null,
                        Line: null,
                        Column: null,
                        Message: trimmedLine);
                }

                continue;
            }

            if (trimmedLine.StartsWith("Project has invalid dependencies:", StringComparison.OrdinalIgnoreCase))
            {
                summary = $"Marker={trimmedLine}";
                primaryDiagnostic = new DaemonPrimaryDiagnostic(
                    Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.PackageResolution,
                    Code: null,
                    File: null,
                    Line: null,
                    Column: null,
                    Message: trimmedLine);
                continue;
            }

            summary = $"FirstError={trimmedLine}";
            primaryDiagnostic = new DaemonPrimaryDiagnostic(
                Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.PackageResolution,
                Code: null,
                File: null,
                Line: null,
                Column: null,
                Message: trimmedLine);
            return true;
        }

        return markerFound;
    }

    private static bool TryGetUserActionRequiredSummary (
        string logText,
        out string summary,
        out DaemonPrimaryDiagnostic? primaryDiagnostic)
    {
        summary = string.Empty;
        primaryDiagnostic = null;

        var lines = logText.Split('\n');
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.Length == 0)
            {
                continue;
            }

            if (!IsUserActionRequiredLine(trimmedLine))
            {
                continue;
            }

            summary = $"Marker={trimmedLine}";
            primaryDiagnostic = new DaemonPrimaryDiagnostic(
                Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.UnityDialog,
                Code: null,
                File: null,
                Line: null,
                Column: null,
                Message: trimmedLine);
            return true;
        }

        return false;
    }

    private static bool TryParseCompilerDiagnostic (
        string line,
        out DaemonPrimaryDiagnostic? diagnostic)
    {
        const string errorMarker = "): error ";
        var closeLocationIndex = line.IndexOf(errorMarker, StringComparison.Ordinal);
        if (closeLocationIndex <= 0)
        {
            diagnostic = null;
            return false;
        }

        var openLocationIndex = line.LastIndexOf('(', closeLocationIndex);
        if (openLocationIndex <= 0)
        {
            diagnostic = null;
            return false;
        }

        var commaIndex = line.IndexOf(',', openLocationIndex + 1);
        if (commaIndex < 0 || commaIndex > closeLocationIndex)
        {
            diagnostic = null;
            return false;
        }

        if (!int.TryParse(
                line.AsSpan(openLocationIndex + 1, commaIndex - openLocationIndex - 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var lineNumber)
            || lineNumber <= 0)
        {
            diagnostic = null;
            return false;
        }

        if (!int.TryParse(
                line.AsSpan(commaIndex + 1, closeLocationIndex - commaIndex - 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var columnNumber)
            || columnNumber <= 0)
        {
            diagnostic = null;
            return false;
        }

        var codeStartIndex = closeLocationIndex + errorMarker.Length;
        var codeEndIndex = line.IndexOf(':', codeStartIndex);
        if (codeEndIndex <= codeStartIndex)
        {
            diagnostic = null;
            return false;
        }

        var code = line[codeStartIndex..codeEndIndex].Trim();
        if (!IsCompilerErrorCode(code))
        {
            diagnostic = null;
            return false;
        }

        diagnostic = new DaemonPrimaryDiagnostic(
            Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler,
            Code: code,
            File: line[..openLocationIndex],
            Line: lineNumber,
            Column: columnNumber,
            Message: line[(codeEndIndex + 1)..].TrimStart());
        return true;
    }

    private static bool IsCompilerErrorCode (string code)
    {
        if (code.Length <= 2 || !code.StartsWith("CS", StringComparison.Ordinal))
        {
            return false;
        }

        for (var i = 2; i < code.Length; i++)
        {
            if (!char.IsDigit(code[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static string? TryExtractCompilerErrorCode (string line)
    {
        var errorIndex = line.IndexOf("error CS", StringComparison.OrdinalIgnoreCase);
        if (errorIndex < 0)
        {
            return null;
        }

        var codeStart = errorIndex + "error ".Length;
        var codeEnd = line.IndexOf(':', codeStart);
        if (codeEnd <= codeStart)
        {
            return null;
        }

        return line[codeStart..codeEnd].Trim();
    }

    private static bool IsUserActionRequiredLine (string line)
    {
        return line.Contains("Safe Mode", StringComparison.OrdinalIgnoreCase)
            || line.Contains("modal dialog", StringComparison.OrdinalIgnoreCase)
            || line.Contains("No valid Unity Editor license", StringComparison.OrdinalIgnoreCase)
            || line.Contains("License is not active", StringComparison.OrdinalIgnoreCase)
            || line.Contains("license activation", StringComparison.OrdinalIgnoreCase)
            || line.Contains("terms of service", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Accept Terms", StringComparison.OrdinalIgnoreCase);
    }
}
