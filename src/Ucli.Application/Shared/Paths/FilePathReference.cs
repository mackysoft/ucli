using System.Diagnostics.CodeAnalysis;

namespace MackySoft.Ucli.Application.Shared.Paths;

/// <summary>Represents an absolute path or a path resolved relative to an explicit root.</summary>
internal sealed record FilePathReference
{
    private FilePathReference (
        AbsolutePath? absolutePath,
        RootRelativePath? rootRelativePath)
    {
        Absolute = absolutePath;
        Relative = rootRelativePath;
    }

    private AbsolutePath? Absolute { get; }

    private RootRelativePath? Relative { get; }

    /// <summary>Parses an absolute or root-relative path.</summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value" /> is neither form.</exception>
    public static FilePathReference Parse (string value)
    {
        if (TryParse(value, out var path))
        {
            return path;
        }

        throw new ArgumentException(
            "Path must be absolute or relative without traversing above its root.",
            nameof(value));
    }

    /// <summary>Attempts to parse an absolute or root-relative path.</summary>
    public static bool TryParse (
        string? value,
        [NotNullWhen(true)] out FilePathReference? path)
    {
        path = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (AbsolutePath.TryParse(value, out var absolutePath, out _))
        {
            path = new FilePathReference(absolutePath, null);
            return true;
        }

        if (RootRelativePath.TryParse(value, out var rootRelativePath, out _))
        {
            path = new FilePathReference(null, rootRelativePath);
            return true;
        }

        return false;
    }

    /// <summary>Resolves the path, using <paramref name="relativeRoot" /> only for relative input.</summary>
    public AbsolutePath ResolveAgainst (AbsolutePath relativeRoot)
    {
        ArgumentNullException.ThrowIfNull(relativeRoot);
        return Absolute ?? ContainedPath.Create(relativeRoot, Relative!).Target;
    }

    /// <summary>Resolves the path while requiring the result to remain under <paramref name="boundaryRoot" />.</summary>
    public bool TryResolveContained (
        AbsolutePath boundaryRoot,
        [NotNullWhen(true)] out ContainedPath? resolvedPath)
    {
        ArgumentNullException.ThrowIfNull(boundaryRoot);
        if (Absolute is not null)
        {
            return ContainedPath.TryCreate(
                boundaryRoot,
                Absolute,
                out resolvedPath,
                out _);
        }

        resolvedPath = ContainedPath.Create(boundaryRoot, Relative!);
        return true;
    }

    /// <inheritdoc />
    public override string ToString () =>
        Absolute?.Value ?? Relative!.Value;
}
