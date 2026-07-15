using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Input;

/// <summary> Reads public uCLI result JSON used by <c>verify --from</c>. </summary>
internal static class VerifyFromInputReader
{
    /// <summary> Reads and normalizes one verify input file. </summary>
    public static VerifyFromInputReadResult Read (
        string json,
        ProjectFingerprint expectedProjectFingerprint)
    {
        ArgumentNullException.ThrowIfNull(expectedProjectFingerprint);

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
        ProjectFingerprint expectedProjectFingerprint)
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
            || !ProjectFingerprint.TryParse(projectFingerprintElement.GetString(), out var projectFingerprint))
        {
            return Failure("The --from payload.project.projectFingerprint is missing.", VerifyErrorCodes.VerifyInputProjectMissing);
        }

        if (projectFingerprint != expectedProjectFingerprint)
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

        if (!TryReadPostReadSource(payload, out var postReadSourceByOpId)
            || !TryAttachPostReadSource(opResults, postReadSourceByOpId, out opResults))
        {
            return Failure("The --from payload.postReadSource contains malformed or unaligned source facts.", VerifyErrorCodes.VerifyInputPayloadInvalid);
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
            && ContractLiteralCodec.Matches(statusElement.GetString(), CommandResultStatus.Ok)
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
                || !TryReadExecuteStepId(opResultElement, "opId", out var opId)
                || !TryReadString(opResultElement, "op", out var op)
                || !TryReadContractLiteral(opResultElement, "phase", out IpcExecuteOperationPhase _)
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
                diagnostics,
                new VerifyFromPostReadSourceStep(
                    opId,
                    IpcExecutePostReadSourceKind.Operation,
                    false,
                    null,
                    false,
                    IpcExecuteExpectedPostState.Unavailable)));
        }

        opResults = results;
        return true;
    }

    private static bool TryReadPostReadSource (
        JsonElement payload,
        out IReadOnlyDictionary<IpcExecuteStepId, VerifyFromPostReadSourceStep> postReadSourceByOpId)
    {
        postReadSourceByOpId = new Dictionary<IpcExecuteStepId, VerifyFromPostReadSourceStep>();
        if (!payload.TryGetProperty("postReadSource", out var postReadSourceElement)
            || postReadSourceElement.ValueKind != JsonValueKind.Object
            || !postReadSourceElement.TryGetProperty("schemaVersion", out var schemaVersionElement)
            || schemaVersionElement.ValueKind != JsonValueKind.Number
            || !schemaVersionElement.TryGetInt32(out var schemaVersion)
            || schemaVersion != IpcExecutePostReadSource.CurrentSchemaVersion
            || !postReadSourceElement.TryGetProperty("steps", out var stepsElement)
            || stepsElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var stepsByOpId = new Dictionary<IpcExecuteStepId, VerifyFromPostReadSourceStep>();
        foreach (var stepElement in stepsElement.EnumerateArray())
        {
            if (stepElement.ValueKind != JsonValueKind.Object
                || !TryReadExecuteStepId(stepElement, "opId", out var opId)
                || !TryReadContractLiteral(stepElement, "sourceKind", out IpcExecutePostReadSourceKind sourceKind)
                || !TryReadBoolean(stepElement, "playModeMutation", out var playModeMutation)
                || !TryReadOptionalPostReadCommit(stepElement, out var commit)
                || !TryReadBoolean(stepElement, "persistenceExpected", out var persistenceExpected)
                || !TryReadContractLiteral(stepElement, "expectedPostState", out IpcExecuteExpectedPostState expectedPostState)
                || stepsByOpId.ContainsKey(opId))
            {
                return false;
            }

            stepsByOpId.Add(opId, new VerifyFromPostReadSourceStep(
                opId,
                sourceKind,
                playModeMutation,
                commit,
                persistenceExpected,
                expectedPostState));
        }

        postReadSourceByOpId = stepsByOpId;
        return true;
    }

    private static bool TryAttachPostReadSource (
        IReadOnlyList<VerifyFromOperationResult> opResults,
        IReadOnlyDictionary<IpcExecuteStepId, VerifyFromPostReadSourceStep> postReadSourceByOpId,
        out IReadOnlyList<VerifyFromOperationResult> normalizedOpResults)
    {
        normalizedOpResults = [];
        if (postReadSourceByOpId.Count != opResults.Count)
        {
            return false;
        }

        var normalizedResults = new VerifyFromOperationResult[opResults.Count];
        for (var i = 0; i < opResults.Count; i++)
        {
            var opResult = opResults[i];
            if (!postReadSourceByOpId.TryGetValue(opResult.OpId, out var sourceStep))
            {
                return false;
            }

            if (!IpcExecutePostReadSourceRules.IsCompatibleWithOperation(
                    opResult.Op,
                    sourceStep.SourceKind,
                    sourceStep.PlayModeMutation,
                    sourceStep.Commit,
                    sourceStep.PersistenceExpected,
                    sourceStep.ExpectedPostState))
            {
                return false;
            }

            normalizedResults[i] = opResult with { PostReadSource = sourceStep };
        }

        normalizedOpResults = normalizedResults;
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
                || !TryReadString(diagnosticElement, "code", out var codeText)
                || !UcliCode.TryCreate(codeText, out var code)
                || !TryReadContractLiteral(diagnosticElement, "severity", out UcliDiagnosticSeverity severity)
                || !TryReadContractLiteral(diagnosticElement, "coverageImpact", out IpcExecuteDiagnosticCoverageImpact coverageImpact)
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
                || !TryReadContractLiteral(requirement, "surface", out IpcExecuteReadPostconditionSurface _)
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

    private static bool TryReadExecuteStepId (
        JsonElement owner,
        string propertyName,
        out IpcExecuteStepId value)
    {
        value = default!;
        if (!TryReadString(owner, propertyName, out var text)
            || StringValueValidator.HasOuterWhitespace(text)
            || !StringValueValidator.IsWellFormedUtf16(text))
        {
            return false;
        }

        value = new IpcExecuteStepId(text);
        return true;
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

    private static bool TryReadOptionalPostReadCommit (
        JsonElement owner,
        out IpcExecutePostReadCommit? value)
    {
        value = null;
        if (!owner.TryGetProperty("commit", out var propertyElement))
        {
            return false;
        }

        if (propertyElement.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (propertyElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        if (!ContractLiteralCodec.TryParse(propertyElement.GetString(), out IpcExecutePostReadCommit commit))
        {
            return false;
        }

        value = commit;
        return true;
    }

    private static bool TouchedItemsAreValid (JsonElement touchedElement)
    {
        foreach (var item in touchedElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !TryReadContractLiteral<UcliTouchedResourceKind>(item, "kind", out _)
                || !TryReadString(item, "path", out _))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadContractLiteral<TEnum> (
        JsonElement owner,
        string propertyName,
        out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        return owner.TryGetProperty(propertyName, out var propertyElement)
            && propertyElement.ValueKind == JsonValueKind.String
            && ContractLiteralCodec.TryParse(propertyElement.GetString(), out value);
    }

    private static VerifyFromInputReadResult Failure (
        string message,
        UcliCode code)
    {
        return VerifyFromInputReadResult.Failure(ApplicationFailure.InvalidInput(message, code));
    }
}
