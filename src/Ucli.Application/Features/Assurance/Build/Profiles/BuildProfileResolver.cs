using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Resolves build profile JSON into build execution input. </summary>
internal static class BuildProfileResolver
{
    private const int CurrentSchemaVersion = 1;

    private static readonly HashSet<string> AllowedRootProperties = new(StringComparer.Ordinal)
    {
        "schemaVersion",
        "inputs",
        "runner",
        "policy",
    };

    private static readonly HashSet<string> AllowedInputsProperties = new(StringComparer.Ordinal)
    {
        "kind",
        "buildTarget",
        "scenes",
        "options",
    };

    private static readonly HashSet<string> AllowedScenesProperties = new(StringComparer.Ordinal)
    {
        "source",
        "paths",
    };

    private static readonly HashSet<string> AllowedOptionsProperties = new(StringComparer.Ordinal)
    {
        "development",
    };

    private static readonly HashSet<string> AllowedRunnerProperties = new(StringComparer.Ordinal)
    {
        "kind",
    };

    private static readonly HashSet<string> AllowedPolicyProperties = new(StringComparer.Ordinal)
    {
        "runtime",
        "projectMutationMode",
    };

    private static readonly HashSet<string> AllowedRuntimeProperties = new(StringComparer.Ordinal)
    {
        "allowedExecutionModes",
        "allowedEditorModes",
    };

    /// <summary> Resolves one build profile from raw JSON text. </summary>
    public static BuildProfileResolutionResult ResolveJson (string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return InvalidProfile("Build profile JSON must not be empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return ResolveCore(document.RootElement);
        }
        catch (JsonException exception)
        {
            return InvalidProfile($"Build profile JSON is invalid. {exception.Message}");
        }
    }

    private static BuildProfileResolutionResult ResolveCore (JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return InvalidProfile("Build profile root must be an object.");
        }

        if (!TryValidateObjectProperties(root, AllowedRootProperties, "Build profile", out var error))
        {
            return error!;
        }

        if (!TryReadSchemaVersion(root, out var schemaVersion, out error)
            || !TryReadInputs(root, out var inputs, out error)
            || !TryReadRunner(root, out var runner, out error)
            || !TryReadPolicy(root, out var policy, out error))
        {
            return error!;
        }

        var digest = BuildProfileDigestCalculator.Calculate(
            schemaVersion,
            inputs!,
            runner!,
            policy!);
        return BuildProfileResolutionResult.Success(new ResolvedBuildProfile(
            SchemaVersion: schemaVersion,
            Inputs: inputs!,
            Runner: runner!,
            Policy: policy!,
            Digest: digest));
    }

    private static bool TryReadSchemaVersion (
        JsonElement root,
        out int schemaVersion,
        out BuildProfileResolutionResult? error)
    {
        if (!JsonObjectPropertyReader.TryReadRequiredInt32(
            root,
            "schemaVersion",
            CreateMissingRequiredPropertyError,
            CreateInt32TypeMismatchError,
            noError: null,
            out schemaVersion,
            out var errorMessage))
        {
            error = InvalidProfile(errorMessage!);
            return false;
        }

        if (schemaVersion != CurrentSchemaVersion)
        {
            error = InvalidProfile($"Build profile schemaVersion must be {CurrentSchemaVersion}. Actual: {schemaVersion}.");
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryReadInputs (
        JsonElement root,
        out ResolvedBuildInputs? inputs,
        out BuildProfileResolutionResult? error)
    {
        inputs = null;
        if (!TryReadRequiredObject(root, "inputs", "Build profile", out var inputsElement, out error)
            || !TryValidateObjectProperties(inputsElement, AllowedInputsProperties, "Build profile inputs", out error)
            || !TryReadRequiredEnum(inputsElement, "kind", "Build profile inputs", out BuildProfileInputsKind kind, out error))
        {
            return false;
        }

        if (kind != BuildProfileInputsKind.Explicit)
        {
            error = InvalidProfile($"Build profile inputs.kind is unsupported: {ContractLiteralCodec.ToValue(kind)}.");
            return false;
        }

        if (!TryReadBuildTarget(inputsElement, out var buildTarget, out error)
            || !TryReadScenes(inputsElement, out var scenes, out error)
            || !TryReadOptions(inputsElement, out var options, out error))
        {
            return false;
        }

        inputs = new ResolvedBuildInputs(kind, buildTarget!, scenes!, options!);
        error = null;
        return true;
    }

    private static bool TryReadBuildTarget (
        JsonElement inputsElement,
        out ResolvedBuildTarget? buildTarget,
        out BuildProfileResolutionResult? error)
    {
        buildTarget = null;
        if (!TryReadRequiredString(inputsElement, "buildTarget", "Build profile inputs", out var stableName, out error))
        {
            return false;
        }

        if (!BuildTargetStableNameCodec.TryResolve(stableName, out var resolvedTarget))
        {
            error = BuildProfileResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"Build profile inputs.buildTarget is unsupported: {stableName}.",
                BuildErrorCodes.BuildTargetUnsupported));
            return false;
        }

        buildTarget = resolvedTarget;
        error = null;
        return true;
    }

    private static bool TryReadScenes (
        JsonElement inputsElement,
        out ResolvedBuildScenes? scenes,
        out BuildProfileResolutionResult? error)
    {
        scenes = null;
        if (!TryReadRequiredObject(inputsElement, "scenes", "Build profile inputs", out var scenesElement, out error)
            || !TryValidateObjectProperties(scenesElement, AllowedScenesProperties, "Build profile inputs.scenes", out error)
            || !TryReadRequiredEnum(scenesElement, "source", "Build profile inputs.scenes", out BuildProfileSceneSource source, out error))
        {
            return false;
        }

        if (source == BuildProfileSceneSource.EditorBuildSettings)
        {
            if (scenesElement.TryGetProperty("paths", out _))
            {
                error = InvalidProfile($"Build profile inputs.scenes.paths must not be specified when source is {ContractLiteralCodec.ToValue(BuildProfileSceneSource.EditorBuildSettings)}.");
                return false;
            }

            scenes = new ResolvedBuildScenes(BuildProfileSceneSource.EditorBuildSettings, Array.Empty<string>());
            error = null;
            return true;
        }

        if (source != BuildProfileSceneSource.Explicit)
        {
            error = InvalidProfile($"Build profile inputs.scenes.source is unsupported: {ContractLiteralCodec.ToValue(source)}.");
            return false;
        }

        return TryReadExplicitScenes(scenesElement, out scenes, out error);
    }

    private static bool TryReadExplicitScenes (
        JsonElement scenesElement,
        out ResolvedBuildScenes? scenes,
        out BuildProfileResolutionResult? error)
    {
        scenes = null;
        if (!TryReadRequiredStringArray(
            scenesElement,
            "paths",
            "Build profile inputs.scenes",
            out var paths,
            out error))
        {
            return false;
        }

        if (paths.Length == 0)
        {
            error = InvalidProfile("Build profile inputs.scenes.paths must contain at least one path.");
            return false;
        }

        var seenPaths = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < paths.Length; i++)
        {
            if (!IsRequiredStringValue(paths[i]))
            {
                error = InvalidProfile($"Build profile inputs.scenes.paths[{i}] must be a non-empty string without outer whitespace, NUL, or newline characters.");
                return false;
            }

            if (!UnityAssetPathContract.IsNormalizedSceneAssetPath(paths[i]))
            {
                error = InvalidProfile($"Build profile inputs.scenes.paths[{i}] must be a project-relative scene asset path under Assets ending with '{UnityAssetPathContract.SceneAssetExtension}'.");
                return false;
            }

            if (!seenPaths.Add(paths[i]))
            {
                error = InvalidProfile($"Build profile inputs.scenes.paths contains duplicate value '{paths[i]}'.");
                return false;
            }
        }

        scenes = new ResolvedBuildScenes(BuildProfileSceneSource.Explicit, paths);
        error = null;
        return true;
    }

    private static bool TryReadOptions (
        JsonElement inputsElement,
        out ResolvedBuildOptions? options,
        out BuildProfileResolutionResult? error)
    {
        options = null;
        if (!TryReadRequiredObject(inputsElement, "options", "Build profile inputs", out var optionsElement, out error)
            || !TryValidateObjectProperties(optionsElement, AllowedOptionsProperties, "Build profile inputs.options", out error))
        {
            return false;
        }

        if (!JsonObjectPropertyReader.TryReadRequiredProperty(
            optionsElement,
            "development",
            static propertyName => $"Build profile inputs.options is missing required property '{propertyName}'.",
            out var developmentElement,
            out var errorMessage))
        {
            error = InvalidProfile(errorMessage);
            return false;
        }

        if (developmentElement.ValueKind != JsonValueKind.True && developmentElement.ValueKind != JsonValueKind.False)
        {
            error = InvalidProfile("Build profile inputs.options.development must be a boolean.");
            return false;
        }

        options = new ResolvedBuildOptions(developmentElement.GetBoolean());
        error = null;
        return true;
    }

    private static bool TryReadRunner (
        JsonElement root,
        out ResolvedBuildRunner? runner,
        out BuildProfileResolutionResult? error)
    {
        runner = null;
        if (!TryReadRequiredObject(root, "runner", "Build profile", out var runnerElement, out error)
            || !TryValidateObjectProperties(runnerElement, AllowedRunnerProperties, "Build profile runner", out error)
            || !TryReadRequiredEnum(runnerElement, "kind", "Build profile runner", out BuildProfileRunnerKind kind, out error))
        {
            return false;
        }

        if (kind != BuildProfileRunnerKind.BuildPipeline)
        {
            error = InvalidProfile($"Build profile runner.kind is unsupported: {ContractLiteralCodec.ToValue(kind)}.");
            return false;
        }

        runner = new ResolvedBuildRunner(kind);
        error = null;
        return true;
    }

    private static bool TryReadPolicy (
        JsonElement root,
        out ResolvedBuildPolicy? policy,
        out BuildProfileResolutionResult? error)
    {
        policy = null;
        if (!TryReadRequiredObject(root, "policy", "Build profile", out var policyElement, out error)
            || !TryValidateObjectProperties(policyElement, AllowedPolicyProperties, "Build profile policy", out error)
            || !TryReadRuntimePolicy(policyElement, out var runtime, out error)
            || !TryReadRequiredEnum(policyElement, "projectMutationMode", "Build profile policy", out BuildProfileProjectMutationMode projectMutationMode, out error))
        {
            return false;
        }

        policy = new ResolvedBuildPolicy(runtime!, projectMutationMode);
        error = null;
        return true;
    }

    private static bool TryReadRuntimePolicy (
        JsonElement policyElement,
        out ResolvedBuildRuntimePolicy? runtime,
        out BuildProfileResolutionResult? error)
    {
        runtime = null;
        if (!TryReadRequiredObject(policyElement, "runtime", "Build profile policy", out var runtimeElement, out error)
            || !TryValidateObjectProperties(runtimeElement, AllowedRuntimeProperties, "Build profile policy.runtime", out error)
            || !TryReadRequiredEnumArray(runtimeElement, "allowedExecutionModes", "Build profile policy.runtime", out IReadOnlyList<BuildProfileRuntimeExecutionMode>? allowedExecutionModes, out error)
            || !TryReadRequiredEnumArray(runtimeElement, "allowedEditorModes", "Build profile policy.runtime", out IReadOnlyList<DaemonEditorMode>? allowedEditorModes, out error))
        {
            return false;
        }

        runtime = new ResolvedBuildRuntimePolicy(allowedExecutionModes!, allowedEditorModes!);
        error = null;
        return true;
    }

    private static bool TryReadRequiredObject (
        JsonElement parent,
        string propertyName,
        string objectName,
        out JsonElement jsonObject,
        out BuildProfileResolutionResult? error)
    {
        if (!JsonObjectPropertyReader.TryReadRequiredProperty(
            parent,
            propertyName,
            CreateMissingRequiredPropertyError,
            out jsonObject,
            out var errorMessage))
        {
            error = InvalidProfile(errorMessage);
            return false;
        }

        if (jsonObject.ValueKind != JsonValueKind.Object)
        {
            error = InvalidProfile($"{objectName} property '{propertyName}' must be an object.");
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryReadRequiredString (
        JsonElement jsonObject,
        string propertyName,
        string objectName,
        out string value,
        out BuildProfileResolutionResult? error)
    {
        if (!JsonObjectPropertyReader.TryReadRequiredString(
            jsonObject,
            propertyName,
            property => $"{objectName} is missing required property '{property}'.",
            property => $"{objectName}.{property} must be string.",
            noError: null,
            out value,
            out var errorMessage))
        {
            error = InvalidProfile(errorMessage!);
            return false;
        }

        if (!IsRequiredStringValue(value))
        {
            error = InvalidProfile($"{objectName}.{propertyName} must be a non-empty string without outer whitespace, NUL, or newline characters.");
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryReadRequiredStringArray (
        JsonElement jsonObject,
        string propertyName,
        string objectName,
        out string[] values,
        out BuildProfileResolutionResult? error)
    {
        if (!JsonObjectPropertyReader.TryReadRequiredStringArray(
            jsonObject,
            propertyName,
            property => $"{objectName} is missing required property '{property}'.",
            property => $"{objectName}.{property} must be string array.",
            property => $"{objectName}.{property} must be string array.",
            noError: null,
            out values,
            out var errorMessage))
        {
            error = InvalidProfile(errorMessage!);
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryReadRequiredEnum<TEnum> (
        JsonElement jsonObject,
        string propertyName,
        string objectName,
        out TEnum value,
        out BuildProfileResolutionResult? error)
        where TEnum : struct, Enum
    {
        value = default;
        if (!TryReadRequiredString(jsonObject, propertyName, objectName, out var literal, out error))
        {
            return false;
        }

        if (!ContractLiteralCodec.TryParse(literal, out value))
        {
            error = InvalidProfile($"{objectName}.{propertyName} is unsupported: {literal}.");
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryReadRequiredEnumArray<TEnum> (
        JsonElement jsonObject,
        string propertyName,
        string objectName,
        out IReadOnlyList<TEnum>? values,
        out BuildProfileResolutionResult? error)
        where TEnum : struct, Enum
    {
        values = null;
        if (!TryReadRequiredStringArray(jsonObject, propertyName, objectName, out var literals, out error))
        {
            return false;
        }

        if (literals.Length == 0)
        {
            error = InvalidProfile($"{objectName}.{propertyName} must contain at least one value.");
            return false;
        }

        var parsedValues = new List<TEnum>(literals.Length);
        var seenValues = new HashSet<TEnum>();
        for (var i = 0; i < literals.Length; i++)
        {
            var literal = literals[i];
            if (!IsRequiredStringValue(literal))
            {
                error = InvalidProfile($"{objectName}.{propertyName}[{i}] must be a non-empty string without outer whitespace, NUL, or newline characters.");
                return false;
            }

            if (!ContractLiteralCodec.TryParse(literal, out TEnum value))
            {
                error = InvalidProfile($"{objectName}.{propertyName}[{i}] is unsupported: {literal}.");
                return false;
            }

            if (!seenValues.Add(value))
            {
                error = InvalidProfile($"{objectName}.{propertyName} contains duplicate value '{literal}'.");
                return false;
            }

            parsedValues.Add(value);
        }

        values = parsedValues;
        error = null;
        return true;
    }

    private static BuildProfileResolutionResult InvalidProfile (string message)
    {
        return BuildProfileResolutionResult.Failure(ExecutionError.InvalidArgument(
            message,
            BuildErrorCodes.BuildProfileInvalid));
    }

    private static bool TryValidateObjectProperties (
        JsonElement jsonObject,
        ISet<string> allowedProperties,
        string objectName,
        out BuildProfileResolutionResult? error)
    {
        var seenProperties = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in jsonObject.EnumerateObject())
        {
            if (!seenProperties.Add(property.Name))
            {
                error = InvalidProfile($"{objectName} contains duplicate property '{property.Name}'.");
                return false;
            }

            if (!allowedProperties.Contains(property.Name))
            {
                error = InvalidProfile($"{objectName} contains unknown property '{property.Name}'.");
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool IsRequiredStringValue (string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !StringValueValidator.HasOuterWhitespace(value)
            && value.IndexOf('\0') < 0
            && value.IndexOf('\n') < 0
            && value.IndexOf('\r') < 0;
    }

    private static string CreateMissingRequiredPropertyError (string propertyName)
    {
        return $"Build profile is missing required property '{propertyName}'.";
    }

    private static string CreateInt32TypeMismatchError (string propertyName)
    {
        return $"Build profile property '{propertyName}' must be int32.";
    }
}
