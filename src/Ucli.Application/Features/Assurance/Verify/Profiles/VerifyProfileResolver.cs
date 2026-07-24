using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;

/// <summary> Resolves built-in and user-authored verify profiles. </summary>
internal static class VerifyProfileResolver
{
    private const string BuiltInDefault = "built-in:default";
    private const string BuiltInMutation = "built-in:mutation";
    private const string BuiltInProject = "built-in:project";
    private const string BuiltInScript = "built-in:script";

    /// <summary> Resolves the effective verify profile. </summary>
    public static VerifyProfileResolutionResult Resolve (
        string? profile)
    {
        return ResolveBuiltInProfile(StringValueNormalizer.TrimToNull(profile) ?? BuiltInDefault);
    }

    /// <summary> Resolves a user-authored file profile from already-read JSON. </summary>
    public static VerifyProfileResolutionResult ResolveFileProfileJson (
        string json,
        string repositoryRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRelativePath);

        try
        {
            using var document = JsonDocument.Parse(json);
            return ReadFileProfile(document.RootElement, repositoryRelativePath);
        }
        catch (JsonException exception)
        {
            return VerifyProfileResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"Verify profile JSON is invalid. {exception.Message}"));
        }
    }

    private static VerifyProfileResolutionResult ResolveBuiltInProfile (string profile)
    {
        return profile switch
        {
            BuiltInDefault => CreateBuiltIn(BuiltInDefault, includeCompile: true, readyTarget: ReadyTarget.Execution, includePostRead: true),
            BuiltInMutation => CreateBuiltIn(BuiltInMutation, includeCompile: false, readyTarget: ReadyTarget.Mutation, includePostRead: true),
            BuiltInProject => CreateBuiltIn(BuiltInProject, includeCompile: true, readyTarget: ReadyTarget.Execution, includePostRead: true),
            BuiltInScript => CreateBuiltIn(BuiltInScript, includeCompile: true, readyTarget: ReadyTarget.Execution, includePostRead: false),
            _ => VerifyProfileResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"Unsupported verify profile '{profile}'. Built-in profiles are {BuiltInDefault}, {BuiltInMutation}, {BuiltInProject}, and {BuiltInScript}.")),
        };
    }

    private static VerifyProfileResolutionResult CreateBuiltIn (
        string profileName,
        bool includeCompile,
        ReadyTarget readyTarget,
        bool includePostRead)
    {
        var steps = new List<VerifyProfileStep>
        {
            VerifyProfileStep.CreateReady(required: true, readyTarget),
        };

        if (includeCompile)
        {
            steps.Add(VerifyProfileStep.CreateCompile(required: true));
        }

        if (includePostRead)
        {
            steps.Add(VerifyProfileStep.CreatePostRead(required: false));
        }

        steps.Add(VerifyProfileStep.CreateLogs());
        return VerifyProfileResolutionResult.Success(new VerifyProfileDefinition(
            VerifyProfileSource.BuiltIn,
            profileName,
            RepositoryRelativePath: null,
            steps));
    }

    private static VerifyProfileResolutionResult ReadFileProfile (
        JsonElement root,
        string repositoryRelativePath)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return InvalidProfile("Verify profile root must be an object.");
        }

        var allowedRootProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "schemaVersion",
            "name",
            "steps",
        };
        if (!ValidateKnownProperties(root, allowedRootProperties, out var unknownRootProperty))
        {
            return InvalidProfile($"Unsupported verify profile property '{unknownRootProperty}'.");
        }

        if (!root.TryGetProperty("schemaVersion", out var schemaVersionElement)
            || schemaVersionElement.ValueKind != JsonValueKind.Number
            || !schemaVersionElement.TryGetInt32(out var schemaVersion)
            || schemaVersion != 1)
        {
            return InvalidProfile("Verify profile schemaVersion must be 1.");
        }

        if (!root.TryGetProperty("steps", out var stepsElement) || stepsElement.ValueKind != JsonValueKind.Array)
        {
            return InvalidProfile("Verify profile steps must be an array.");
        }

        var name = ReadOptionalString(root, "name") ?? repositoryRelativePath;
        var steps = new List<VerifyProfileStep>();
        var index = 0;
        foreach (var stepElement in stepsElement.EnumerateArray())
        {
            var stepResult = ReadFileProfileStep(stepElement, index);
            if (!stepResult.IsSuccess)
            {
                return VerifyProfileResolutionResult.Failure(stepResult.Error!);
            }

            steps.Add(stepResult.Step!);
            index++;
        }

        var duplicateStepKind = steps
            .GroupBy(static step => step.Kind)
            .Where(static group => group.Skip(1).Any())
            .FirstOrDefault();
        if (duplicateStepKind != null)
        {
            return InvalidProfile(
                $"Verify profile contains duplicate step kind '{TextVocabulary.GetText(duplicateStepKind.Key)}'.");
        }

        return VerifyProfileResolutionResult.Success(new VerifyProfileDefinition(
            VerifyProfileSource.File,
            name,
            repositoryRelativePath,
            steps));
    }

    private static VerifyProfileStepReadResult ReadFileProfileStep (
        JsonElement stepElement,
        int index)
    {
        if (stepElement.ValueKind != JsonValueKind.Object)
        {
            return VerifyProfileStepReadResult.Failure(InvalidProfileError($"Verify profile steps[{index}] must be an object."));
        }

        if (!TryReadRequiredString(stepElement, "kind", out var kindValue))
        {
            return VerifyProfileStepReadResult.Failure(InvalidProfileError($"Verify profile steps[{index}].kind is required."));
        }

        if (!TextVocabulary.TryGetValue(kindValue, out VerifyStepKind kind))
        {
            return VerifyProfileStepReadResult.Failure(InvalidProfileError($"Verify profile step kind is unsupported: {kindValue}."));
        }

        var allowedProperties = CreateAllowedStepProperties(kind);
        if (!ValidateKnownProperties(stepElement, allowedProperties, out var unknownStepProperty))
        {
            return VerifyProfileStepReadResult.Failure(InvalidProfileError(
                $"Unsupported verify profile steps[{index}] property '{unknownStepProperty}'."));
        }

        if (!stepElement.TryGetProperty("required", out var requiredElement)
            || (requiredElement.ValueKind != JsonValueKind.True && requiredElement.ValueKind != JsonValueKind.False))
        {
            return VerifyProfileStepReadResult.Failure(InvalidProfileError(
                $"Verify profile steps[{index}].required must be a boolean."));
        }

        var required = requiredElement.GetBoolean();
        if (kind == VerifyStepKind.Logs && required)
        {
            return VerifyProfileStepReadResult.Failure(InvalidProfileError("The logs verifier must be required=false."));
        }

        var stepResult = ReadTypedStep(stepElement, kind, required, index);
        if (!stepResult.IsSuccess)
        {
            return stepResult;
        }

        var step = stepResult.Step!;
        if (stepElement.TryGetProperty("effects", out var effectsElement)
            && !EffectsMatch(effectsElement, step.Effects))
        {
            return VerifyProfileStepReadResult.Failure(InvalidProfileError(
                $"Verify profile steps[{index}].effects does not match uCLI computed effects."));
        }

        return VerifyProfileStepReadResult.Success(step);
    }

    private static VerifyProfileStepReadResult ReadTypedStep (
        JsonElement stepElement,
        VerifyStepKind kind,
        bool required,
        int index)
    {
        return kind switch
        {
            VerifyStepKind.Ready => ReadReadyStep(stepElement, required, index),
            VerifyStepKind.Compile => VerifyProfileStepReadResult.Success(VerifyProfileStep.CreateCompile(required)),
            VerifyStepKind.PostRead => VerifyProfileStepReadResult.Success(VerifyProfileStep.CreatePostRead(required)),
            VerifyStepKind.Test => ReadTestStep(stepElement, required, index),
            VerifyStepKind.Logs => VerifyProfileStepReadResult.Success(VerifyProfileStep.CreateLogs()),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Verify step kind must be defined."),
        };
    }

    private static VerifyProfileStepReadResult ReadReadyStep (
        JsonElement stepElement,
        bool required,
        int index)
    {
        var readyTarget = ReadyTarget.Execution;
        if (stepElement.TryGetProperty("target", out var targetElement))
        {
            if (targetElement.ValueKind != JsonValueKind.String
                || targetElement.GetString() is not { } targetValue
                || !VocabularyInputParser.TryParseIgnoreCase(targetValue, out readyTarget))
            {
                return VerifyProfileStepReadResult.Failure(InvalidProfileError(
                    $"Verify profile steps[{index}].target is invalid."));
            }
        }

        return VerifyProfileStepReadResult.Success(VerifyProfileStep.CreateReady(required, readyTarget));
    }

    private static VerifyProfileStepReadResult ReadTestStep (
        JsonElement stepElement,
        bool required,
        int index)
    {
        TestRunPlatform? testPlatform = null;
        if (stepElement.TryGetProperty("testPlatform", out var testPlatformElement))
        {
            if (testPlatformElement.ValueKind != JsonValueKind.String
                || !TestRunPlatformCodec.TryParse(testPlatformElement.GetString(), out var parsedTestPlatform))
            {
                return VerifyProfileStepReadResult.Failure(InvalidProfileError(
                    $"Verify profile steps[{index}].testPlatform is invalid."));
            }

            testPlatform = parsedTestPlatform;
        }

        if (stepElement.TryGetProperty("testFilter", out var testFilterElement)
            && testFilterElement.ValueKind is not JsonValueKind.String and not JsonValueKind.Null)
        {
            return VerifyProfileStepReadResult.Failure(InvalidProfileError(
                $"Verify profile steps[{index}].testFilter must be a string."));
        }

        if (!TryReadOptionalStringArray(stepElement, "testCategory", out var testCategory, out var error)
            || !TryReadOptionalStringArray(stepElement, "assemblyName", out var assemblyName, out error))
        {
            return VerifyProfileStepReadResult.Failure(error!);
        }

        return VerifyProfileStepReadResult.Success(VerifyProfileStep.CreateTest(
            required,
            testPlatform,
            ReadOptionalString(stepElement, "testFilter"),
            testCategory,
            assemblyName));
    }

    private static HashSet<string> CreateAllowedStepProperties (VerifyStepKind kind)
    {
        var properties = new HashSet<string>(StringComparer.Ordinal)
        {
            "kind",
            "required",
            "effects",
        };

        if (kind == VerifyStepKind.Ready)
        {
            properties.Add("target");
        }

        if (kind == VerifyStepKind.Test)
        {
            properties.Add("testPlatform");
            properties.Add("testFilter");
            properties.Add("testCategory");
            properties.Add("assemblyName");
        }

        return properties;
    }

    private static bool EffectsMatch (
        JsonElement effectsElement,
        IReadOnlyList<AssuranceEffect> expectedEffects)
    {
        if (effectsElement.ValueKind != JsonValueKind.Array || effectsElement.GetArrayLength() != expectedEffects.Count)
        {
            return false;
        }

        var index = 0;
        foreach (var effectElement in effectsElement.EnumerateArray())
        {
            if (effectElement.ValueKind != JsonValueKind.String
                || !TextVocabulary.TryGetValue(effectElement.GetString(), out AssuranceEffect effect)
                || effect != expectedEffects[index])
            {
                return false;
            }

            index++;
        }

        return true;
    }

    private static bool ValidateKnownProperties (
        JsonElement owner,
        IReadOnlySet<string> allowedProperties,
        out string unknownProperty)
    {
        foreach (var property in owner.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
            {
                unknownProperty = property.Name;
                return false;
            }
        }

        unknownProperty = string.Empty;
        return true;
    }

    private static bool TryReadRequiredString (
        JsonElement owner,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        if (!owner.TryGetProperty(propertyName, out var propertyElement) || propertyElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        if (!StringValueNormalizer.TryTrimToNonEmpty(propertyElement.GetString() ?? string.Empty, out var normalizedValue))
        {
            return false;
        }

        value = normalizedValue;
        return true;
    }

    private static string? ReadOptionalString (
        JsonElement owner,
        string propertyName)
    {
        return owner.TryGetProperty(propertyName, out var propertyElement) && propertyElement.ValueKind == JsonValueKind.String
            ? StringValueNormalizer.TrimToNull(propertyElement.GetString())
            : null;
    }

    private static bool TryReadOptionalStringArray (
        JsonElement owner,
        string propertyName,
        out string[]? values,
        out ExecutionError? error)
    {
        values = null;
        error = null;
        if (!owner.TryGetProperty(propertyName, out var arrayElement) || arrayElement.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (arrayElement.ValueKind != JsonValueKind.Array)
        {
            error = InvalidProfileError($"Verify profile {propertyName} must be an array.");
            return false;
        }

        var result = new List<string>();
        foreach (var item in arrayElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String
                || !StringValueNormalizer.TryTrimToNonEmpty(item.GetString(), out var value))
            {
                error = InvalidProfileError($"Verify profile {propertyName} must contain non-empty strings.");
                return false;
            }

            result.Add(value);
        }

        values = result.ToArray();
        return true;
    }

    private static VerifyProfileResolutionResult InvalidProfile (string message)
    {
        return VerifyProfileResolutionResult.Failure(InvalidProfileError(message));
    }

    private static ExecutionError InvalidProfileError (string message)
    {
        return ExecutionError.InvalidArgument(message);
    }

    private sealed record VerifyProfileStepReadResult (
        VerifyProfileStep? Step,
        ExecutionError? Error)
    {
        public bool IsSuccess => Error is null;

        public static VerifyProfileStepReadResult Success (VerifyProfileStep step)
        {
            ArgumentNullException.ThrowIfNull(step);
            return new VerifyProfileStepReadResult(step, null);
        }

        public static VerifyProfileStepReadResult Failure (ExecutionError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new VerifyProfileStepReadResult(null, error);
        }
    }
}
