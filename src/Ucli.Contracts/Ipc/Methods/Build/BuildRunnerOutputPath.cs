using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a portable path relative to a build runner output directory. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
public sealed class BuildRunnerOutputPath : UcliStringValue
{
    /// <summary> Initializes a normalized build runner output path. </summary>
    /// <param name="value"> The relative path. Directory separators are normalized to <c>/</c>. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value" /> is empty, rooted, contains outer whitespace, or contains an empty, current-directory, or parent-directory segment.
    /// </exception>
    [JsonConstructor]
    public BuildRunnerOutputPath (string value)
        : base(NormalizeOrThrow(value))
    {
    }

    /// <summary> Attempts to parse and normalize a build runner output path. </summary>
    /// <param name="value"> The candidate path. </param>
    /// <param name="path"> The normalized path when parsing succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when <paramref name="value" /> is a portable relative path; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        [NotNullWhen(true)] out BuildRunnerOutputPath? path)
    {
        path = null;
        if (!RelativePathContract.TryNormalize(value, out var normalizedPath))
        {
            return false;
        }

        path = new BuildRunnerOutputPath(normalizedPath);
        return true;
    }

    private static string NormalizeOrThrow (string? value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (!RelativePathContract.TryNormalize(value, out var normalizedPath))
        {
            throw new ArgumentException(
                "Build runner output path must be a portable path relative to the runner output directory.",
                nameof(value));
        }

        return normalizedPath;
    }
}
