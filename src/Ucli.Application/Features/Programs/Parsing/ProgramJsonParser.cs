using System.Buffers;
using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Programs.Parsing;

/// <summary> Parses the fixed Program step union and reports RFC 6901 input locations. </summary>
internal sealed class ProgramJsonParser : IProgramJsonParser
{
    private const string InvalidJsonCode = "program.invalidJson";
    private const string RootTypeMismatchCode = "program.rootTypeMismatch";
    private const string UnknownPropertyCode = "program.unknownProperty";
    private const string DuplicatePropertyCode = "program.duplicateProperty";
    private const string MissingPropertyCode = "program.missingProperty";
    private const string TypeMismatchCode = "program.typeMismatch";
    private const string InvalidValueCode = "program.invalidValue";
    private const string ExclusivePropertyCode = "program.exclusiveProperty";

    public ProgramJsonParseResult Parse (string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Failure(InvalidJsonCode, null, "Program JSON must not be empty.");
        }

        return Parse(Encoding.UTF8.GetBytes(json));
    }

    public ProgramJsonParseResult Parse (ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.IsEmpty)
        {
            return Failure(InvalidJsonCode, null, "Program JSON must not be empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(utf8Json.ToArray());
            return ParseRoot(document.RootElement);
        }
        catch (JsonException exception)
        {
            return Failure(InvalidJsonCode, null, $"Program JSON is invalid. {exception.Message}");
        }
    }

    private static ProgramJsonParseResult ParseRoot (JsonElement root)
    {
        var diagnostics = new List<ProgramDiagnostic>();
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Failure(RootTypeMismatchCode, string.Empty, "Program JSON root must be an object.");
        }

        ValidateObjectProperties(root, ["steps"], string.Empty, diagnostics);
        if (!root.TryGetProperty("steps", out var steps))
        {
            Add(diagnostics, MissingPropertyCode, "/steps", "Program property 'steps' is required.");
        }
        else if (steps.ValueKind != JsonValueKind.Array)
        {
            Add(diagnostics, TypeMismatchCode, "/steps", "Program property 'steps' must be an array.");
        }
        else if (steps.GetArrayLength() == 0)
        {
            Add(diagnostics, InvalidValueCode, "/steps", "Program property 'steps' must not be empty.");
        }

        if (diagnostics.Count > 0)
        {
            return ProgramJsonParseResult.Failure(diagnostics);
        }

        var parsedSteps = new List<ProgramStep>(steps.GetArrayLength());
        var index = 0;
        foreach (var step in steps.EnumerateArray())
        {
            var stepResult = ParseStep(step, $"/steps/{index}");
            if (stepResult.Step is null)
            {
                diagnostics.AddRange(stepResult.Diagnostics);
            }
            else
            {
                parsedSteps.Add(stepResult.Step);
            }

            index++;
        }

        return diagnostics.Count == 0
            ? ProgramJsonParseResult.Success(new ProgramDefinition(parsedSteps, root.Clone()))
            : ProgramJsonParseResult.Failure(diagnostics);
    }

    private static StepParseResult ParseStep (JsonElement element, string path)
    {
        var diagnostics = new List<ProgramDiagnostic>();
        if (element.ValueKind != JsonValueKind.Object)
        {
            Add(diagnostics, TypeMismatchCode, path, "Program step must be an object.");
            return new StepParseResult(null, diagnostics);
        }

        var command = ReadRequiredString(element, "command", path, diagnostics);
        if (command is null)
        {
            return new StepParseResult(null, diagnostics);
        }

        var timeout = ReadTimeout(element, path, diagnostics);
        ProgramStep? step = command switch
        {
            "call" => ParseCall(element, path, timeout, diagnostics),
            "ready" => ParseEmptyStep<ReadyProgramStep>(element, path, timeout, diagnostics, static value => new ReadyProgramStep(value)),
            "refresh" => ParseEmptyStep<RefreshProgramStep>(element, path, timeout, diagnostics, static value => new RefreshProgramStep(value)),
            "compile" => ParseEmptyStep<CompileProgramStep>(element, path, timeout, diagnostics, static value => new CompileProgramStep(value)),
            "play.enter" => ParseEmptyStep<PlayEnterProgramStep>(element, path, timeout, diagnostics, static value => new PlayEnterProgramStep(value)),
            "play.exit" => ParseEmptyStep<PlayExitProgramStep>(element, path, timeout, diagnostics, static value => new PlayExitProgramStep(value)),
            "screenshot.game" => ParseScreenshotGame(element, path, timeout, diagnostics),
            "screenshot.scene" => ParseEmptyStep<ScreenshotSceneProgramStep>(element, path, timeout, diagnostics, static value => new ScreenshotSceneProgramStep(value)),
            _ => null,
        };

        if (step is null && command is not ("call" or "ready" or "refresh" or "compile" or "play.enter" or "play.exit" or "screenshot.game" or "screenshot.scene"))
        {
            Add(diagnostics, InvalidValueCode, $"{path}/command", $"Program command '{command}' is not supported.");
        }

        return diagnostics.Count == 0
            ? new StepParseResult(step, diagnostics)
            : new StepParseResult(null, diagnostics);
    }

    private static ProgramStep? ParseCall (JsonElement element, string path, int? timeout, List<ProgramDiagnostic> diagnostics)
    {
        ValidateObjectProperties(element, ["command", "timeoutMilliseconds", "steps", "requestPath"], path, diagnostics);
        var hasSteps = element.TryGetProperty("steps", out var inlineSteps);
        var hasRequestPath = element.TryGetProperty("requestPath", out var requestPathElement);
        if (hasSteps == hasRequestPath)
        {
            Add(diagnostics, ExclusivePropertyCode, path, "Program call step must specify exactly one of 'steps' or 'requestPath'.");
            return null;
        }

        if (hasRequestPath)
        {
            var requestPath = ReadRequiredString(element, "requestPath", path, diagnostics);
            if (requestPath is not null && string.IsNullOrWhiteSpace(requestPath))
            {
                Add(diagnostics, InvalidValueCode, $"{path}/requestPath", "Program requestPath must not be empty.");
            }

            return diagnostics.Count == 0 ? new CallProgramStep(timeout, null, requestPath) : null;
        }

        if (inlineSteps.ValueKind != JsonValueKind.Array)
        {
            Add(diagnostics, TypeMismatchCode, $"{path}/steps", "Program call steps must be an array.");
            return null;
        }

        using var request = CreateInlineRequestDocument(inlineSteps);
        var requestResult = new ValidateRequestJsonParser().Parse(request.RootElement.GetRawText());
        if (!requestResult.IsSuccess)
        {
            Add(diagnostics, InvalidValueCode, $"{path}/steps", requestResult.Error!.Message);
            return null;
        }

        return new CallProgramStep(timeout, requestResult.Request, null);
    }

    private static JsonDocument CreateInlineRequestDocument (JsonElement steps)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("protocolVersion", IpcProtocol.CurrentVersion);
            writer.WritePropertyName("steps");
            steps.WriteTo(writer);
            writer.WriteEndObject();
        }

        return JsonDocument.Parse(buffer.WrittenMemory);
    }

    private static ProgramStep? ParseScreenshotGame (JsonElement element, string path, int? timeout, List<ProgramDiagnostic> diagnostics)
    {
        ValidateObjectProperties(element, ["command", "timeoutMilliseconds", "width", "height"], path, diagnostics);
        var width = ReadOptionalPositiveInt32(element, "width", path, diagnostics);
        var height = ReadOptionalPositiveInt32(element, "height", path, diagnostics);
        if ((width is null) != (height is null))
        {
            Add(diagnostics, ExclusivePropertyCode, path, "Program screenshot.game width and height must be specified together.");
        }

        return diagnostics.Count == 0 ? new ScreenshotGameProgramStep(timeout, width, height) : null;
    }

    private static TStep? ParseEmptyStep<TStep> (
        JsonElement element,
        string path,
        int? timeout,
        List<ProgramDiagnostic> diagnostics,
        Func<int?, TStep> create)
        where TStep : ProgramStep
    {
        ValidateObjectProperties(element, ["command", "timeoutMilliseconds"], path, diagnostics);
        return diagnostics.Count == 0 ? create(timeout) : null;
    }

    private static int? ReadTimeout (JsonElement element, string path, List<ProgramDiagnostic> diagnostics)
    {
        if (!element.TryGetProperty("timeoutMilliseconds", out var timeout))
        {
            return null;
        }

        if (timeout.ValueKind != JsonValueKind.Number || !timeout.TryGetInt32(out var value))
        {
            Add(diagnostics, TypeMismatchCode, $"{path}/timeoutMilliseconds", "Program timeoutMilliseconds must be an integer.");
            return null;
        }

        if (value < 1)
        {
            Add(diagnostics, InvalidValueCode, $"{path}/timeoutMilliseconds", "Program timeoutMilliseconds must be between 1 and 2147483647.");
            return null;
        }

        return value;
    }

    private static int? ReadOptionalPositiveInt32 (JsonElement element, string name, string path, List<ProgramDiagnostic> diagnostics)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var value))
        {
            Add(diagnostics, TypeMismatchCode, $"{path}/{name}", $"Program {name} must be an integer.");
            return null;
        }

        if (value < 1)
        {
            Add(diagnostics, InvalidValueCode, $"{path}/{name}", $"Program {name} must be positive.");
            return null;
        }

        return value;
    }

    private static string? ReadRequiredString (JsonElement element, string name, string path, List<ProgramDiagnostic> diagnostics)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            Add(diagnostics, MissingPropertyCode, $"{path}/{name}", $"Program property '{name}' is required.");
            return null;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            Add(diagnostics, TypeMismatchCode, $"{path}/{name}", $"Program property '{name}' must be a string.");
            return null;
        }

        return property.GetString() ?? string.Empty;
    }

    private static void ValidateObjectProperties (JsonElement element, string[] allowedProperties, string path, List<ProgramDiagnostic> diagnostics)
    {
        var allowed = new HashSet<string>(allowedProperties, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            var propertyPath = $"{path}/{EscapePointerToken(property.Name)}";
            if (!seen.Add(property.Name))
            {
                Add(diagnostics, DuplicatePropertyCode, propertyPath, $"Program JSON contains duplicate property '{property.Name}'.");
            }

            if (!allowed.Contains(property.Name))
            {
                Add(diagnostics, UnknownPropertyCode, propertyPath, $"Program JSON contains unknown property '{property.Name}'.");
            }
        }
    }

    private static string EscapePointerToken (string value) => value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    private static void Add (List<ProgramDiagnostic> diagnostics, string code, string? instancePath, string message)
    {
        diagnostics.Add(new ProgramDiagnostic(code, instancePath, message));
    }

    private static ProgramJsonParseResult Failure (string code, string? instancePath, string message)
    {
        return ProgramJsonParseResult.Failure([new ProgramDiagnostic(code, instancePath, message)]);
    }

    private sealed record StepParseResult (ProgramStep? Step, IReadOnlyList<ProgramDiagnostic> Diagnostics);
}
