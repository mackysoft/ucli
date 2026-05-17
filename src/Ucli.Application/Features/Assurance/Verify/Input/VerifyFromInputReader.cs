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

        var readPostconditionRequirementCount = ReadPostconditionRequirementCount(payload);
        return VerifyFromInputReadResult.Success(new VerifyFromInput(
            command,
            projectFingerprint,
            opResults,
            readPostconditionRequirementCount));
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
                || !TryReadString(diagnosticElement, "coverageImpact", out var coverageImpact)
                || !TryReadString(diagnosticElement, "message", out var message))
            {
                return null;
            }

            diagnostics.Add(new VerifyFromDiagnostic(code, coverageImpact, message));
        }

        return diagnostics;
    }

    private static int ReadPostconditionRequirementCount (JsonElement payload)
    {
        return payload.TryGetProperty("readPostcondition", out var readPostcondition)
            && readPostcondition.ValueKind == JsonValueKind.Object
            && readPostcondition.TryGetProperty("requirements", out var requirements)
            && requirements.ValueKind == JsonValueKind.Array
            ? requirements.GetArrayLength()
            : 0;
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

    private static VerifyFromInputReadResult Failure (
        string message,
        UcliCode code)
    {
        return VerifyFromInputReadResult.Failure(ApplicationFailure.InvalidInput(message, code));
    }
}
