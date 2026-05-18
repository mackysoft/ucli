using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Input;

/// <summary> Reads public uCLI result JSON used by <c>verify --from</c>. </summary>
internal static class VerifyFromInputReader
{
    /// <summary> Reads and normalizes one verify input file. </summary>
    public static VerifyFromInputReadResult Read (
        string json,
        string expectedProjectFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedProjectFingerprint);

        if (string.IsNullOrWhiteSpace(json))
        {
            return Failure("The --from JSON must not be empty.", VerifyErrorCodes.VerifyInputPayloadInvalid);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return ReadCore(document.RootElement, expectedProjectFingerprint);
        }
        catch (JsonException exception)
        {
            return Failure($"The --from JSON is invalid. {exception.Message}", VerifyErrorCodes.VerifyInputPayloadInvalid);
        }
    }

    private static VerifyFromInputReadResult ReadCore (
        JsonElement root,
        string expectedProjectFingerprint)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Failure("The --from root must be an object.", VerifyErrorCodes.VerifyInputSchemaUnsupported);
        }

        if (!root.TryGetProperty("protocolVersion", out var protocolVersionElement)
            || protocolVersionElement.ValueKind != JsonValueKind.Number
            || !protocolVersionElement.TryGetInt32(out var protocolVersion)
            || protocolVersion != IpcProtocol.CurrentVersion)
        {
            return Failure("The --from protocolVersion does not match the current uCLI protocol version.", VerifyErrorCodes.VerifyInputProtocolVersionMismatch);
        }

        if (!IsSuccessfulPublicResult(root))
        {
            return Failure("The --from result must be a successful public uCLI result with status=ok, exitCode=0, and no errors.", VerifyErrorCodes.VerifyInputPayloadInvalid);
        }

        if (!root.TryGetProperty("command", out var commandElement) || commandElement.ValueKind != JsonValueKind.String)
        {
            return Failure("The --from command is missing.", VerifyErrorCodes.VerifyInputCommandUnsupported);
        }

        var command = commandElement.GetString() ?? string.Empty;
        if (command is not ("call" or "refresh"))
        {
            return Failure($"The --from command is unsupported for verify: {command}.", VerifyErrorCodes.VerifyInputCommandUnsupported);
        }

        if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
        {
            return Failure("The --from payload is missing or invalid.", VerifyErrorCodes.VerifyInputPayloadInvalid);
        }

        if (!payload.TryGetProperty("project", out var project) || project.ValueKind != JsonValueKind.Object
            || !project.TryGetProperty("projectFingerprint", out var projectFingerprintElement)
            || projectFingerprintElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(projectFingerprintElement.GetString()))
        {
            return Failure("The --from payload.project.projectFingerprint is missing.", VerifyErrorCodes.VerifyInputProjectMissing);
        }

        var projectFingerprint = projectFingerprintElement.GetString()!;
        if (!string.Equals(projectFingerprint, expectedProjectFingerprint, StringComparison.Ordinal))
        {
            return Failure(
                $"The --from project fingerprint does not match the resolved project. Expected={expectedProjectFingerprint}, Actual={projectFingerprint}.",
                VerifyErrorCodes.ProjectFingerprintMismatch);
        }

        if (!payload.TryGetProperty("opResults", out var opResultsElement) || opResultsElement.ValueKind != JsonValueKind.Array)
        {
            return Failure("The --from payload.opResults must be an array.", VerifyErrorCodes.VerifyInputPayloadInvalid);
        }

        if (!TryReadOpResults(opResultsElement, out var opResults))
        {
            return Failure("The --from payload.opResults contains malformed operation results.", VerifyErrorCodes.VerifyInputPayloadInvalid);
        }

        if (!TryReadPostconditionRequirementCount(payload, out var readPostconditionRequirementCount))
        {
            return Failure("The --from payload.readPostcondition contains malformed requirements.", VerifyErrorCodes.VerifyInputPayloadInvalid);
        }

        return VerifyFromInputReadResult.Success(new VerifyFromInput(
            command,
            projectFingerprint,
            opResults,
            readPostconditionRequirementCount));
    }

    private static bool IsSuccessfulPublicResult (JsonElement root)
    {
        return root.TryGetProperty("status", out var statusElement)
            && statusElement.ValueKind == JsonValueKind.String
            && string.Equals(statusElement.GetString(), IpcProtocol.StatusOk, StringComparison.Ordinal)
            && root.TryGetProperty("exitCode", out var exitCodeElement)
            && exitCodeElement.ValueKind == JsonValueKind.Number
            && exitCodeElement.TryGetInt32(out var exitCode)
            && exitCode == 0
            && root.TryGetProperty("errors", out var errorsElement)
            && errorsElement.ValueKind == JsonValueKind.Array
            && errorsElement.GetArrayLength() == 0;
    }

    private static bool TryReadOpResults (
        JsonElement opResultsElement,
        out IReadOnlyList<VerifyFromOperationResult> opResults)
    {
        var results = new List<VerifyFromOperationResult>();
        foreach (var opResultElement in opResultsElement.EnumerateArray())
        {
            if (opResultElement.ValueKind != JsonValueKind.Object
                || !TryReadString(opResultElement, "opId", out var opId)
                || !TryReadString(opResultElement, "op", out var op)
                || !TryReadKnownOperationPhase(opResultElement, "phase")
                || !TryReadBoolean(opResultElement, "applied", out var applied)
                || !TryReadBoolean(opResultElement, "changed", out var changed)
                || !opResultElement.TryGetProperty("touched", out var touchedElement)
                || touchedElement.ValueKind != JsonValueKind.Array)
            {
                opResults = [];
                return false;
            }

            var diagnostics = ReadDiagnostics(opResultElement);
            if (diagnostics is null)
            {
                opResults = [];
                return false;
            }

            if (!TouchedItemsAreValid(touchedElement))
            {
                opResults = [];
                return false;
            }

            results.Add(new VerifyFromOperationResult(
                opId,
                op,
                applied,
                changed,
                touchedElement.GetArrayLength(),
                diagnostics));
        }

        opResults = results;
        return true;
    }

    private static IReadOnlyList<VerifyFromDiagnostic>? ReadDiagnostics (JsonElement opResultElement)
    {
        if (!opResultElement.TryGetProperty("diagnostics", out var diagnosticsElement)
            || diagnosticsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var diagnostics = new List<VerifyFromDiagnostic>();
        foreach (var diagnosticElement in diagnosticsElement.EnumerateArray())
        {
            if (diagnosticElement.ValueKind != JsonValueKind.Object
                || !TryReadString(diagnosticElement, "code", out var code)
                || !UcliCode.IsValidValue(code)
                || !TryReadKnownDiagnosticSeverity(diagnosticElement, "severity", out var severity)
                || !TryReadString(diagnosticElement, "coverageImpact", out var coverageImpact)
                || !IsKnownDiagnosticCoverageImpact(coverageImpact)
                || !TryReadString(diagnosticElement, "message", out var message))
            {
                return null;
            }

            diagnostics.Add(new VerifyFromDiagnostic(code, severity, coverageImpact, message));
        }

        return diagnostics;
    }

    private static bool TryReadPostconditionRequirementCount (
        JsonElement payload,
        out int count)
    {
        count = 0;
        if (!payload.TryGetProperty("readPostcondition", out var readPostcondition) || readPostcondition.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (readPostcondition.ValueKind != JsonValueKind.Object
            || !readPostcondition.TryGetProperty("requirements", out var requirements)
            || requirements.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var requirement in requirements.EnumerateArray())
        {
            if (requirement.ValueKind != JsonValueKind.Object
                || !TryReadString(requirement, "surface", out var surface)
                || !IsKnownReadPostconditionSurface(surface)
                || !TryReadString(requirement, "minSafeGeneratedAtUtc", out var minSafeGeneratedAtUtc)
                || !IpcIso8601TimestampCodec.TryParseOptionalWithTimezoneOffset(minSafeGeneratedAtUtc, out var parsedMinSafeGeneratedAtUtc)
                || parsedMinSafeGeneratedAtUtc is null)
            {
                return false;
            }
        }

        count = requirements.GetArrayLength();
        return true;
    }

    private static bool TryReadString (
        JsonElement owner,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        return owner.TryGetProperty(propertyName, out var propertyElement)
            && propertyElement.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value = propertyElement.GetString() ?? string.Empty);
    }

    private static bool TryReadBoolean (
        JsonElement owner,
        string propertyName,
        out bool value)
    {
        value = false;
        if (!owner.TryGetProperty(propertyName, out var propertyElement))
        {
            return false;
        }

        if (propertyElement.ValueKind == JsonValueKind.True || propertyElement.ValueKind == JsonValueKind.False)
        {
            value = propertyElement.GetBoolean();
            return true;
        }

        return false;
    }

    private static bool TryReadKnownOperationPhase (
        JsonElement owner,
        string propertyName)
    {
        return TryReadString(owner, propertyName, out var phase)
            && phase is IpcExecuteOperationPhaseNames.Validate
                or IpcExecuteOperationPhaseNames.Plan
                or IpcExecuteOperationPhaseNames.Call
                or IpcExecuteOperationPhaseNames.Skipped;
    }

    private static bool TouchedItemsAreValid (JsonElement touchedElement)
    {
        foreach (var item in touchedElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !TryReadString(item, "kind", out var kind)
                || !IsKnownTouchedResourceKind(kind)
                || !TryReadString(item, "path", out _))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadKnownDiagnosticSeverity (
        JsonElement owner,
        string propertyName,
        out string severity)
    {
        if (TryReadString(owner, propertyName, out severity)
            && severity is IpcExecuteDiagnosticSeverityNames.Info
                or IpcExecuteDiagnosticSeverityNames.Warning
                or IpcExecuteDiagnosticSeverityNames.Error)
        {
            return true;
        }

        severity = string.Empty;
        return false;
    }

    private static bool IsKnownDiagnosticCoverageImpact (string coverageImpact)
    {
        return coverageImpact is IpcExecuteDiagnosticCoverageImpactNames.None
            or IpcExecuteDiagnosticCoverageImpactNames.Partial
            or IpcExecuteDiagnosticCoverageImpactNames.Indeterminate;
    }

    private static bool IsKnownTouchedResourceKind (string kind)
    {
        return kind is IpcExecuteTouchedResourceKindNames.Scene
            or IpcExecuteTouchedResourceKindNames.Prefab
            or IpcExecuteTouchedResourceKindNames.Asset
            or IpcExecuteTouchedResourceKindNames.ProjectSettings;
    }

    private static bool IsKnownReadPostconditionSurface (string surface)
    {
        return surface is IpcExecuteReadPostconditionSurfaceNames.AssetSearch
            or IpcExecuteReadPostconditionSurfaceNames.GuidPath
            or IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite;
    }

    private static VerifyFromInputReadResult Failure (
        string message,
        UcliCode code)
    {
        return VerifyFromInputReadResult.Failure(ApplicationFailure.InvalidInput(message, code));
    }
}
