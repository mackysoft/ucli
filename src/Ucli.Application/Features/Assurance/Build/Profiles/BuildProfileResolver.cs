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
        "target",
        "scenes",
        "output",
        "options",
    };

    private static readonly HashSet<string> AllowedScenesProperties = new(StringComparer.Ordinal)
    {
        "source",
        "paths",
    };

    private static readonly HashSet<string> AllowedOutputProperties = new(StringComparer.Ordinal)
    {
        "kind",
    };

    private static readonly HashSet<string> AllowedOptionsProperties = new(StringComparer.Ordinal)
    {
        "development",
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

        if (!TryReadSchemaVersion(root, out var schemaVersion, out error))
        {
            return error!;
        }

        if (!TryReadTarget(root, out var target, out error)
            || !TryReadScenes(root, out var scenes, out error)
            || !TryReadOutput(root, out var output, out error)
            || !TryReadOptions(root, out var options, out error))
        {
            return error!;
        }

        var digest = BuildProfileDigestCalculator.Calculate(
            schemaVersion,
            target!,
            scenes!,
            output!,
            options!);
        return BuildProfileResolutionResult.Success(new ResolvedBuildProfile(
            SchemaVersion: schemaVersion,
            Target: target!,
            Scenes: scenes!,
            Output: output!,
            Options: options!,
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

    private static bool TryReadTarget (
        JsonElement root,
        out ResolvedBuildTarget? target,
        out BuildProfileResolutionResult? error)
    {
        target = null;
        if (!JsonObjectPropertyReader.TryReadRequiredString(
            root,
            "target",
            CreateMissingRequiredPropertyError,
            CreateStringTypeMismatchError,
            noError: null,
            out var stableName,
            out var errorMessage))
        {
            error = InvalidProfile(errorMessage!);
            return false;
        }

        if (string.IsNullOrWhiteSpace(stableName))
        {
            error = InvalidProfile("Build profile target must not be empty.");
            return false;
        }

        if (!BuildTargetStableNameCodec.TryResolve(stableName, out var resolvedTarget))
        {
            error = BuildProfileResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"Build profile target is unsupported: {stableName}.",
                BuildErrorCodes.BuildTargetUnsupported));
            return false;
        }

        target = resolvedTarget;
        error = null;
        return true;
    }

    private static bool TryReadScenes (
        JsonElement root,
        out ResolvedBuildScenes? scenes,
        out BuildProfileResolutionResult? error)
    {
        scenes = null;
        if (!JsonObjectPropertyReader.TryReadRequiredProperty(
            root,
            "scenes",
            CreateMissingRequiredPropertyError,
            out var scenesElement,
            out var errorMessage))
        {
            error = InvalidProfile(errorMessage!);
            return false;
        }

        if (scenesElement.ValueKind != JsonValueKind.Object)
        {
            error = InvalidProfile("Build profile property 'scenes' must be an object.");
            return false;
        }

        if (!TryValidateObjectProperties(scenesElement, AllowedScenesProperties, "Build profile scenes", out error))
        {
            return false;
        }

        if (!JsonObjectPropertyReader.TryReadRequiredString(
            scenesElement,
            "source",
            CreateMissingRequiredScenesPropertyError,
            CreateScenesStringTypeMismatchError,
            noError: null,
            out var source,
            out errorMessage))
        {
            error = InvalidProfile(errorMessage!);
            return false;
        }

        if (!ContractLiteralCodec.TryParse<BuildProfileSceneSource>(source, out var sceneSource))
        {
            error = InvalidProfile($"Build profile scenes.source is unsupported: {source}.");
            return false;
        }

        if (sceneSource == BuildProfileSceneSource.EditorBuildSettings)
        {
            if (scenesElement.TryGetProperty("paths", out _))
            {
                error = InvalidProfile($"Build profile scenes.paths must not be specified when scenes.source is {ContractLiteralCodec.ToValue(BuildProfileSceneSource.EditorBuildSettings)}.");
                return false;
            }

            scenes = new ResolvedBuildScenes(BuildProfileSceneSource.EditorBuildSettings, Array.Empty<string>());
            error = null;
            return true;
        }

        if (sceneSource != BuildProfileSceneSource.Explicit)
        {
            error = InvalidProfile($"Build profile scenes.source is unsupported: {source}.");
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
        if (!JsonObjectPropertyReader.TryReadRequiredStringArray(
            scenesElement,
            "paths",
            CreateMissingRequiredScenesPropertyError,
            CreateScenesStringArrayTypeMismatchError,
            CreateScenesStringArrayTypeMismatchError,
            noError: null,
            out var paths,
            out var errorMessage))
        {
            error = InvalidProfile(errorMessage!);
            return false;
        }

        if (paths.Length == 0)
        {
            error = InvalidProfile("Build profile scenes.paths must contain at least one path.");
            return false;
        }

        for (var i = 0; i < paths.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(paths[i]))
            {
                error = InvalidProfile($"Build profile scenes.paths[{i}] must not be empty.");
                return false;
            }

            if (!UnityAssetPathContract.IsNormalizedSceneAssetPath(paths[i]))
            {
                error = InvalidProfile($"Build profile scenes.paths[{i}] must be a project-relative scene asset path under Assets ending with '{UnityAssetPathContract.SceneAssetExtension}'.");
                return false;
            }
        }

        scenes = new ResolvedBuildScenes(BuildProfileSceneSource.Explicit, paths);
        error = null;
        return true;
    }

    private static bool TryReadOutput (
        JsonElement root,
        out ResolvedBuildOutputPolicy? output,
        out BuildProfileResolutionResult? error)
    {
        output = null;
        if (!JsonObjectPropertyReader.TryReadRequiredProperty(
            root,
            "output",
            CreateMissingRequiredPropertyError,
            out var outputElement,
            out var errorMessage))
        {
            error = InvalidProfile(errorMessage!);
            return false;
        }

        if (outputElement.ValueKind != JsonValueKind.Object)
        {
            error = InvalidProfile("Build profile property 'output' must be an object.");
            return false;
        }

        if (!TryValidateObjectProperties(outputElement, AllowedOutputProperties, "Build profile output", out error))
        {
            return false;
        }

        if (!JsonObjectPropertyReader.TryReadRequiredString(
            outputElement,
            "kind",
            CreateMissingRequiredOutputPropertyError,
            CreateOutputStringTypeMismatchError,
            noError: null,
            out var kind,
            out errorMessage))
        {
            error = InvalidProfile(errorMessage!);
            return false;
        }

        if (!ContractLiteralCodec.TryParse<BuildProfileOutputKind>(kind, out var outputKind)
            || outputKind != BuildProfileOutputKind.UcliArtifact)
        {
            error = InvalidProfile($"Build profile output.kind is unsupported: {kind}.");
            return false;
        }

        output = new ResolvedBuildOutputPolicy(BuildProfileOutputKind.UcliArtifact);
        error = null;
        return true;
    }

    private static bool TryReadOptions (
        JsonElement root,
        out ResolvedBuildOptions? options,
        out BuildProfileResolutionResult? error)
    {
        options = null;
        if (!JsonObjectPropertyReader.TryReadRequiredProperty(
            root,
            "options",
            CreateMissingRequiredPropertyError,
            out var optionsElement,
            out var errorMessage))
        {
            error = InvalidProfile(errorMessage!);
            return false;
        }

        if (optionsElement.ValueKind != JsonValueKind.Object)
        {
            error = InvalidProfile("Build profile property 'options' must be an object.");
            return false;
        }

        if (!TryValidateObjectProperties(optionsElement, AllowedOptionsProperties, "Build profile options", out error))
        {
            return false;
        }

        if (!JsonObjectPropertyReader.TryReadRequiredProperty(
            optionsElement,
            "development",
            CreateMissingRequiredOptionsPropertyError,
            out var developmentElement,
            out errorMessage))
        {
            error = InvalidProfile(errorMessage!);
            return false;
        }

        if (developmentElement.ValueKind != JsonValueKind.True && developmentElement.ValueKind != JsonValueKind.False)
        {
            error = InvalidProfile("Build profile options.development must be a boolean.");
            return false;
        }

        options = new ResolvedBuildOptions(developmentElement.GetBoolean());
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

    private static string CreateMissingRequiredPropertyError (string propertyName)
    {
        return $"Build profile is missing required property '{propertyName}'.";
    }

    private static string CreateMissingRequiredScenesPropertyError (string propertyName)
    {
        return $"Build profile scenes is missing required property '{propertyName}'.";
    }

    private static string CreateMissingRequiredOutputPropertyError (string propertyName)
    {
        return $"Build profile output is missing required property '{propertyName}'.";
    }

    private static string CreateMissingRequiredOptionsPropertyError (string propertyName)
    {
        return $"Build profile options is missing required property '{propertyName}'.";
    }

    private static string CreateInt32TypeMismatchError (string propertyName)
    {
        return $"Build profile property '{propertyName}' must be int32.";
    }

    private static string CreateStringTypeMismatchError (string propertyName)
    {
        return $"Build profile property '{propertyName}' must be string.";
    }

    private static string CreateScenesStringTypeMismatchError (string propertyName)
    {
        return $"Build profile scenes.{propertyName} must be string.";
    }

    private static string CreateScenesStringArrayTypeMismatchError (string propertyName)
    {
        return $"Build profile scenes.{propertyName} must be string array.";
    }

    private static string CreateOutputStringTypeMismatchError (string propertyName)
    {
        return $"Build profile output.{propertyName} must be string.";
    }
}
