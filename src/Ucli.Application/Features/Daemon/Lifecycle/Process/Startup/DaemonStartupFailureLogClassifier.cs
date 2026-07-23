using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;

/// <summary> Classifies daemon startup failures from one Unity startup log segment. </summary>
internal static class DaemonStartupFailureLogClassifier
{
    private const string NuGetForUnityRestoreFailedCode = "NUGET_FOR_UNITY_RESTORE_FAILED";
    private const int UserActionPriority = 1;
    private const int PrecompiledAssemblyConflictPriority = 2;
    private const int UcliPluginDependencyPriority = 3;
    private const int PackageResolutionPriority = 4;
    private const int CompilerPriority = 5;
    private const int BatchmodeSafeModeFallbackPriority = 6;

    /// <summary> Extracts the latest startup log segment from one Unity log text. </summary>
    /// <param name="logText"> The complete Unity log text. </param>
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
    /// <param name="context"> The Unity startup observation context. </param>
    /// <param name="error"> The structured startup failure when classification succeeds. </param>
    /// <returns> <see langword="true" /> when one known startup failure was classified; otherwise, <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="startupLogText" /> is <see langword="null" />. </exception>
    public static bool TryClassify (
        string startupLogText,
        DaemonStartupFailureClassificationContext context,
        out ExecutionError? error)
    {
        ArgumentNullException.ThrowIfNull(startupLogText);

        if (TryClassifyFailure(startupLogText, context, out var classification))
        {
            error = ExecutionError.InternalError(classification.Message);
            return true;
        }

        error = null;
        return false;
    }

    /// <summary> Tries to classify one daemon startup failure from one startup log segment. </summary>
    /// <param name="startupLogText"> The latest Unity startup log segment. </param>
    /// <param name="context"> The Unity startup observation context. </param>
    /// <param name="classification"> The structured startup failure when classification succeeds. </param>
    /// <returns> <see langword="true" /> when one known startup failure was classified; otherwise, <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="startupLogText" /> is <see langword="null" />. </exception>
    public static bool TryClassifyFailure (
        string startupLogText,
        DaemonStartupFailureClassificationContext context,
        [NotNullWhen(true)]
        out DaemonStartupFailureClassification? classification)
    {
        ArgumentNullException.ThrowIfNull(startupLogText);

        var bestCandidate = (DaemonStartupFailureCandidate?)null;
        TryPromoteCandidate(TryCreateUserActionCandidate(startupLogText, context), ref bestCandidate);
        TryPromoteCandidate(TryCreatePrecompiledAssemblyConflictCandidate(startupLogText), ref bestCandidate);
        TryPromoteCandidate(TryCreateUcliPluginDependencyCandidate(startupLogText), ref bestCandidate);
        TryPromoteCandidate(TryCreatePackageResolutionCandidate(startupLogText), ref bestCandidate);
        TryPromoteCandidate(TryCreateCompilerCandidate(startupLogText), ref bestCandidate);

        classification = bestCandidate?.Classification;
        return classification is not null;
    }

    private static void TryPromoteCandidate (
        DaemonStartupFailureCandidate? candidate,
        ref DaemonStartupFailureCandidate? bestCandidate)
    {
        if (candidate is null)
        {
            return;
        }

        if (bestCandidate is null || candidate.Priority < bestCandidate.Priority)
        {
            bestCandidate = candidate;
        }
    }

    private static DaemonStartupFailureCandidate? TryCreateUserActionCandidate (
        string logText,
        DaemonStartupFailureClassificationContext context)
    {
        foreach (var trimmedLine in GetNonEmptyTrimmedLines(logText))
        {
            if (!IsUserActionRequiredLine(trimmedLine))
            {
                continue;
            }

            var safeModeLine = IsSafeModeLine(trimmedLine);
            if (safeModeLine && context == DaemonStartupFailureClassificationContext.Batchmode)
            {
                return new DaemonStartupFailureCandidate(
                    Priority: BatchmodeSafeModeFallbackPriority,
                    Classification: CreateCompileClassification(
                        $"Marker={trimmedLine}",
                        new DaemonPrimaryDiagnostic(
                            Kind: DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
                            Code: null,
                            File: null,
                            Line: null,
                            Column: null,
                            Message: trimmedLine)));
            }

            var startupBlockingReason = safeModeLine
                ? DaemonStartupBlockingReason.SafeMode
                : DaemonStartupBlockingReason.ModalDialog;
            return new DaemonStartupFailureCandidate(
                Priority: UserActionPriority,
                Classification: new DaemonStartupFailureClassification(
                    startupBlockingReason: startupBlockingReason,
                    reason: DaemonDiagnosisReason.EditorUserActionRequired,
                    retryDisposition: DaemonStartupRetryDisposition.ManualActionRequired,
                    message: $"Unity Editor startup is blocked because Unity requires user action. Marker={trimmedLine}",
                    startupPhase: DaemonDiagnosisStartupPhase.UserAction,
                    actionRequired: DaemonDiagnosisActionRequired.ResolveUnityDialog,
                    primaryDiagnostic: new DaemonPrimaryDiagnostic(
                        Kind: DaemonDiagnosisPrimaryDiagnosticKind.UnityDialog,
                        Code: null,
                        File: null,
                        Line: null,
                        Column: null,
                        Message: trimmedLine)));
        }

        return null;
    }

    private static DaemonStartupFailureCandidate? TryCreatePrecompiledAssemblyConflictCandidate (string logText)
    {
        foreach (var trimmedLine in GetNonEmptyTrimmedLines(logText))
        {
            if (!trimmedLine.Contains("Multiple precompiled assemblies with the same name", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return new DaemonStartupFailureCandidate(
                Priority: PrecompiledAssemblyConflictPriority,
                Classification: new DaemonStartupFailureClassification(
                    startupBlockingReason: DaemonStartupBlockingReason.PrecompiledAssemblyConflict,
                    reason: DaemonDiagnosisReason.PrecompiledAssemblyConflict,
                    retryDisposition: DaemonStartupRetryDisposition.RetryAfterFix,
                    message: $"Unity Editor startup is blocked by a precompiled assembly conflict. Marker={trimmedLine}",
                    startupPhase: DaemonDiagnosisStartupPhase.ScriptCompilation,
                    actionRequired: DaemonDiagnosisActionRequired.FixCompileErrors,
                    primaryDiagnostic: new DaemonPrimaryDiagnostic(
                        Kind: DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
                        Code: null,
                        File: null,
                        Line: null,
                        Column: null,
                        Message: trimmedLine)));
        }

        return null;
    }

    private static DaemonStartupFailureCandidate? TryCreateUcliPluginDependencyCandidate (string logText)
    {
        var previousLineWasNuGetForUnityRestoreContext = false;
        foreach (var trimmedLine in GetNonEmptyTrimmedLines(logText))
        {
            var lineIsNuGetForUnityRestoreContext = IsNuGetForUnityRestoreContextLine(trimmedLine);
            if (IsNuGetForUnityRestoreFailureLine(trimmedLine, previousLineWasNuGetForUnityRestoreContext))
            {
                previousLineWasNuGetForUnityRestoreContext = lineIsNuGetForUnityRestoreContext;
                continue;
            }

            if (!IsUcliPluginDependencyMissingLine(trimmedLine))
            {
                previousLineWasNuGetForUnityRestoreContext = lineIsNuGetForUnityRestoreContext;
                continue;
            }

            return new DaemonStartupFailureCandidate(
                Priority: UcliPluginDependencyPriority,
                Classification: new DaemonStartupFailureClassification(
                    startupBlockingReason: DaemonStartupBlockingReason.UcliPlugin,
                    reason: DaemonDiagnosisReason.UcliPluginDependencyMissing,
                    retryDisposition: DaemonStartupRetryDisposition.RetryAfterFix,
                    message: $"Unity Editor startup is blocked because uCLI plugin dependencies are missing. FirstError={trimmedLine}",
                    startupPhase: DaemonDiagnosisStartupPhase.ScriptCompilation,
                    actionRequired: DaemonDiagnosisActionRequired.ResolvePackages,
                    primaryDiagnostic: new DaemonPrimaryDiagnostic(
                        Kind: DaemonDiagnosisPrimaryDiagnosticKind.PluginDependency,
                        Code: null,
                        File: null,
                        Line: null,
                        Column: null,
                        Message: trimmedLine)));
        }

        return null;
    }

    private static DaemonStartupFailureCandidate? TryCreatePackageResolutionCandidate (string logText)
    {
        if (TryGetNuGetForUnityRestoreFailureSummary(logText, out var nugetSummary, out var nugetDiagnostic))
        {
            return new DaemonStartupFailureCandidate(
                Priority: PackageResolutionPriority,
                Classification: CreatePackageResolutionClassification(nugetSummary, nugetDiagnostic));
        }

        if (!TryGetPackageResolutionErrorSummary(logText, out var packageErrorSummary, out var packageDiagnostic))
        {
            return null;
        }

        return new DaemonStartupFailureCandidate(
            Priority: PackageResolutionPriority,
            Classification: CreatePackageResolutionClassification(packageErrorSummary, packageDiagnostic));
    }

    private static DaemonStartupFailureCandidate? TryCreateCompilerCandidate (string logText)
    {
        if (!TryGetCompilerErrorSummary(logText, out var compilerErrorSummary, out var compilerDiagnostic))
        {
            return null;
        }

        return new DaemonStartupFailureCandidate(
            Priority: CompilerPriority,
            Classification: CreateCompileClassification(compilerErrorSummary, compilerDiagnostic));
    }

    private static DaemonStartupFailureClassification CreateCompileClassification (
        string summary,
        DaemonPrimaryDiagnostic? primaryDiagnostic)
    {
        return new DaemonStartupFailureClassification(
            startupBlockingReason: DaemonStartupBlockingReason.Compile,
            reason: DaemonDiagnosisReason.UnityScriptCompilationFailed,
            retryDisposition: DaemonStartupRetryDisposition.RetryAfterFix,
            message: $"Unity Editor startup is blocked because scripts have compiler errors. {summary}",
            startupPhase: DaemonDiagnosisStartupPhase.ScriptCompilation,
            actionRequired: DaemonDiagnosisActionRequired.FixCompileErrors,
            primaryDiagnostic: primaryDiagnostic);
    }

    private static DaemonStartupFailureClassification CreatePackageResolutionClassification (
        string summary,
        DaemonPrimaryDiagnostic? primaryDiagnostic)
    {
        return new DaemonStartupFailureClassification(
            startupBlockingReason: DaemonStartupBlockingReason.PackageResolution,
            reason: DaemonDiagnosisReason.UnityPackageResolutionFailed,
            retryDisposition: DaemonStartupRetryDisposition.RetryAfterFix,
            message: $"Unity Editor startup is blocked because package resolution failed. {summary}",
            startupPhase: DaemonDiagnosisStartupPhase.PackageResolution,
            actionRequired: DaemonDiagnosisActionRequired.ResolvePackages,
            primaryDiagnostic: primaryDiagnostic);
    }

    private static bool TryGetCompilerErrorSummary (
        string logText,
        out string summary,
        out DaemonPrimaryDiagnostic? primaryDiagnostic)
    {
        const string compilerErrorsMarker = "Scripts have compiler errors";
        summary = string.Empty;
        primaryDiagnostic = null;

        foreach (var trimmedLine in GetNonEmptyTrimmedLines(logText))
        {
            if (trimmedLine.Contains("error CS", StringComparison.OrdinalIgnoreCase))
            {
                summary = $"FirstError={trimmedLine}";
                primaryDiagnostic = TryParseCompilerDiagnostic(trimmedLine, out var diagnostic)
                    ? diagnostic
                    : new DaemonPrimaryDiagnostic(
                        Kind: DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
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
                    Kind: DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
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

        var markerFound = false;
        foreach (var trimmedLine in GetNonEmptyTrimmedLines(logText))
        {
            if (!markerFound)
            {
                if (trimmedLine.Contains(packageFailureMarker, StringComparison.OrdinalIgnoreCase)
                    || trimmedLine.Contains("package resolution failed", StringComparison.OrdinalIgnoreCase))
                {
                    markerFound = true;
                    summary = $"Marker={trimmedLine}";
                    primaryDiagnostic = new DaemonPrimaryDiagnostic(
                        Kind: DaemonDiagnosisPrimaryDiagnosticKind.PackageResolution,
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
                    Kind: DaemonDiagnosisPrimaryDiagnosticKind.PackageResolution,
                    Code: null,
                    File: null,
                    Line: null,
                    Column: null,
                    Message: trimmedLine);
                continue;
            }

            summary = $"FirstError={trimmedLine}";
            primaryDiagnostic = new DaemonPrimaryDiagnostic(
                Kind: DaemonDiagnosisPrimaryDiagnosticKind.PackageResolution,
                Code: null,
                File: null,
                Line: null,
                Column: null,
                Message: trimmedLine);
            return true;
        }

        return markerFound;
    }

    private static bool TryGetNuGetForUnityRestoreFailureSummary (
        string logText,
        out string summary,
        out DaemonPrimaryDiagnostic? primaryDiagnostic)
    {
        summary = string.Empty;
        primaryDiagnostic = null;

        var restoreFailureLine = (string?)null;
        var previousLineWasNuGetForUnityRestoreContext = false;
        foreach (var trimmedLine in GetNonEmptyTrimmedLines(logText))
        {
            var lineIsNuGetForUnityRestoreContext = IsNuGetForUnityRestoreContextLine(trimmedLine);
            if (IsNuGetForUnityRestoreFailureLine(trimmedLine, previousLineWasNuGetForUnityRestoreContext))
            {
                restoreFailureLine ??= trimmedLine;
            }

            previousLineWasNuGetForUnityRestoreContext = lineIsNuGetForUnityRestoreContext;
        }

        if (restoreFailureLine is null)
        {
            return false;
        }

        var diagnosticMessage = restoreFailureLine;
        summary = $"FirstError={restoreFailureLine}";
        primaryDiagnostic = new DaemonPrimaryDiagnostic(
            Kind: DaemonDiagnosisPrimaryDiagnosticKind.PackageResolution,
            Code: NuGetForUnityRestoreFailedCode,
            File: null,
            Line: null,
            Column: null,
            Message: diagnosticMessage);
        return true;
    }

    private static string[] GetNonEmptyTrimmedLines (string logText)
    {
        return logText.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
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
            Kind: DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
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
        return IsSafeModeLine(line)
            || line.Contains("modal dialog", StringComparison.OrdinalIgnoreCase)
            || line.Contains("No valid Unity Editor license", StringComparison.OrdinalIgnoreCase)
            || line.Contains("License is not active", StringComparison.OrdinalIgnoreCase)
            || line.Contains("license activation", StringComparison.OrdinalIgnoreCase)
            || line.Contains("terms of service", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Accept Terms", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSafeModeLine (string line)
    {
        return line.Contains("Safe Mode", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRestoreFailureLine (string line)
    {
        return line.Contains("restore", StringComparison.OrdinalIgnoreCase)
            && (line.Contains("failed", StringComparison.OrdinalIgnoreCase)
                || line.Contains("failure", StringComparison.OrdinalIgnoreCase)
                || line.Contains("error", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsNuGetForUnityRestoreFailureLine (
        string line,
        bool previousLineWasNuGetForUnityRestoreContext)
    {
        return IsRestoreFailureLine(line)
            && (line.Contains("NuGetForUnity", StringComparison.OrdinalIgnoreCase)
                || previousLineWasNuGetForUnityRestoreContext);
    }

    private static bool IsNuGetForUnityRestoreContextLine (string line)
    {
        return line.Contains("NuGetForUnity", StringComparison.OrdinalIgnoreCase)
            && line.Contains("Restoring package", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUcliPluginDependencyMissingLine (string line)
    {
        var compilerDiagnosticIndex = line.IndexOf("error CS0234", StringComparison.OrdinalIgnoreCase);
        var ucliPluginSourceIndex = line.IndexOf("MackySoft.Ucli.Unity", StringComparison.Ordinal);
        var isUcliPluginJsonNamespaceDiagnostic = compilerDiagnosticIndex >= 0
            && ucliPluginSourceIndex >= 0
            && ucliPluginSourceIndex < compilerDiagnosticIndex
            && line.Contains("namespace name 'Json'", StringComparison.Ordinal)
            && line.Contains("namespace 'MackySoft'", StringComparison.Ordinal);
        var mentionsUcliDependency = line.Contains("MackySoft.Ucli.Contracts", StringComparison.Ordinal)
            || line.Contains("MackySoft.Ucli.Infrastructure", StringComparison.Ordinal)
            || (line.Contains("MackySoft.Ucli", StringComparison.Ordinal)
                && (line.Contains("Contracts", StringComparison.Ordinal)
                    || line.Contains("Infrastructure", StringComparison.Ordinal)))
            || line.Contains("MackySoft.Json.Canonicalization", StringComparison.Ordinal)
            || (line.Contains("MackySoft.Json", StringComparison.Ordinal)
                && line.Contains("Canonicalization", StringComparison.Ordinal))
            || isUcliPluginJsonNamespaceDiagnostic
            || line.Contains("Rfc8785JsonCanonicalizer", StringComparison.Ordinal)
            || line.Contains("JsonCanonicalizationException", StringComparison.Ordinal)
            || line.Contains("JsonCanonicalizationFailureKind", StringComparison.Ordinal);
        if (!mentionsUcliDependency)
        {
            return false;
        }

        return line.Contains("Could not load file or assembly", StringComparison.OrdinalIgnoreCase)
            || line.Contains("FileNotFoundException", StringComparison.OrdinalIgnoreCase)
            || line.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || line.Contains("missing", StringComparison.OrdinalIgnoreCase)
            || line.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
            || line.Contains("error CS0234", StringComparison.OrdinalIgnoreCase)
            || line.Contains("error CS0246", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record DaemonStartupFailureCandidate (
        int Priority,
        DaemonStartupFailureClassification Classification);
}
