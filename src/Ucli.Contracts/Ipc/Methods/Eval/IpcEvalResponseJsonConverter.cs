using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Serializes the phase-discriminated eval success union without adding a second wire discriminator. </summary>
public sealed class IpcEvalResponseJsonConverter : JsonConverter<IpcEvalResponse>
{
    public override IpcEvalResponse Read (ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Eval response must be an object.");
        }

        EnsureOnlyKnownProperties(root);
        var project = ReadRequiredReference<UnityProjectIdentity>(root, "project", options);
        var phase = ReadRequiredValue<CsEvalPhase>(root, "phase", options);
        var applicationState = ReadRequiredValue<ExecutionApplicationState>(root, "applicationState", options);
        var evalElement = ReadRequiredElement(root, "eval");
        object eval = phase switch
        {
            CsEvalPhase.Plan => Deserialize<CsEvalPlanSuccessResult>(evalElement, options),
            CsEvalPhase.Call => Deserialize<CsEvalCallSuccessResult>(evalElement, options),
            _ => throw new JsonException("Eval response phase is invalid."),
        };
        var planToken = ReadOptional<string>(root, "planToken", options);
        var readPostcondition = ReadOptional<ExecutionReadPostcondition>(root, "readPostcondition", options);
        return new IpcEvalResponse(project, phase, applicationState, eval, planToken, readPostcondition);
    }

    public override void Write (Utf8JsonWriter writer, IpcEvalResponse value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("project");
        JsonSerializer.Serialize(writer, value.Project, options);
        writer.WritePropertyName("phase");
        JsonSerializer.Serialize(writer, value.Phase, options);
        writer.WritePropertyName("applicationState");
        JsonSerializer.Serialize(writer, value.ApplicationState, options);
        writer.WritePropertyName("eval");
        JsonSerializer.Serialize(writer, value.Eval, value.Eval.GetType(), options);
        if (value.PlanToken is not null)
        {
            writer.WriteString("planToken", value.PlanToken);
        }

        if (value.ReadPostcondition is not null)
        {
            writer.WritePropertyName("readPostcondition");
            JsonSerializer.Serialize(writer, value.ReadPostcondition, options);
        }

        writer.WriteEndObject();
    }

    private static void EnsureOnlyKnownProperties (JsonElement root)
    {
        var properties = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!properties.Add(property.Name)
                || property.Name is not "project" and not "phase" and not "applicationState" and not "eval" and not "planToken" and not "readPostcondition")
            {
                throw new JsonException($"Eval response contains an unknown or duplicate property: {property.Name}.");
            }
        }
    }

    private static JsonElement ReadRequiredElement (JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            throw new JsonException($"Eval response requires '{name}'.");
        }

        return value;
    }

    private static T ReadRequiredReference<T> (JsonElement root, string name, JsonSerializerOptions options)
        where T : class
    {
        return Deserialize<T>(ReadRequiredElement(root, name), options);
    }

    private static T ReadRequiredValue<T> (JsonElement root, string name, JsonSerializerOptions options)
        where T : struct
    {
        return Deserialize<T>(ReadRequiredElement(root, name), options);
    }

    private static T? ReadOptional<T> (JsonElement root, string name, JsonSerializerOptions options)
        where T : class
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? Deserialize<T>(value, options)
            : null;
    }

    private static T Deserialize<T> (JsonElement value, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<T>(value.GetRawText(), options)
            ?? throw new JsonException("Eval response value must not be null.");
    }
}
