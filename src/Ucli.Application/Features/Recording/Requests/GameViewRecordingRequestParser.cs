using System.Text.Json;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Presentation;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Application.Features.Recording.Requests;

/// <summary>Parses the closed schema-version-one GameView recording request.</summary>
internal static class GameViewRecordingRequestParser
{
    private static readonly HashSet<string> RootProperties = new(StringComparer.Ordinal)
    {
        "schemaVersion",
        "resolution",
        "frameRate",
        "maxDurationSeconds",
    };

    private static readonly HashSet<string> ResolutionProperties = new(StringComparer.Ordinal)
    {
        "width",
        "height",
    };

    public static GameViewRecordingRequestParseResult Parse (string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8,
            });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Invalid("Recording request root must be an object.");
            }

            var rootError = ValidateClosedObject(root, RootProperties, "Recording request");
            if (rootError is not null)
            {
                return Invalid(rootError);
            }

            if (!TryReadRequiredInt32(root, "schemaVersion", out var schemaVersion)
                || schemaVersion != 1)
            {
                return Invalid("schemaVersion must be the integer 1.");
            }

            if (!root.TryGetProperty("resolution", out var resolution)
                || resolution.ValueKind != JsonValueKind.Object)
            {
                return Invalid("resolution must be an object.");
            }

            var resolutionError = ValidateClosedObject(
                resolution,
                ResolutionProperties,
                "resolution");
            if (resolutionError is not null)
            {
                return Invalid(resolutionError);
            }

            if (!TryReadRequiredInt32(resolution, "width", out var width)
                || width <= 0)
            {
                return Invalid("resolution.width must be a positive integer.");
            }

            if (!TryReadRequiredInt32(resolution, "height", out var height)
                || height <= 0)
            {
                return Invalid("resolution.height must be a positive integer.");
            }

            if (!TryReadRequiredInt32(root, "frameRate", out var frameRate)
                || frameRate <= 0)
            {
                return Invalid("frameRate must be a positive integer.");
            }

            int? maxDurationSeconds = null;
            if (root.TryGetProperty("maxDurationSeconds", out var duration))
            {
                if (duration.ValueKind != JsonValueKind.Number
                    || !duration.TryGetInt32(out var parsedDuration)
                    || parsedDuration <= 0)
                {
                    return Invalid("maxDurationSeconds must be a positive integer when specified.");
                }

                maxDurationSeconds = parsedDuration;
            }

            return GameViewRecordingRequestParseResult.Success(
                new GameViewRecordingRequestDocument(
                    schemaVersion,
                    new PixelDimensions(width, height),
                    frameRate,
                    maxDurationSeconds.HasValue
                        ? UcliOptionalInt32.FromValue(maxDurationSeconds.Value)
                        : default));
        }
        catch (JsonException exception)
        {
            return Invalid($"Recording request JSON is invalid. {exception.Message}");
        }
    }

    private static bool TryReadRequiredInt32 (
        JsonElement source,
        string propertyName,
        out int value)
    {
        value = default;
        return source.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value);
    }

    private static string? ValidateClosedObject (
        JsonElement source,
        IReadOnlySet<string> allowedProperties,
        string subject)
    {
        var observed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in source.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
            {
                return $"{subject} contains unknown property '{property.Name}'.";
            }

            if (!observed.Add(property.Name))
            {
                return $"{subject} contains duplicate property '{property.Name}'.";
            }
        }

        return null;
    }

    private static GameViewRecordingRequestParseResult Invalid (string message) =>
        GameViewRecordingRequestParseResult.Failure(message);
}
