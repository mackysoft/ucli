using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;

/// <summary> Represents one established effective verify profile identity. </summary>
internal abstract record VerifyProfileOutput
{
    protected VerifyProfileOutput (
        string name,
        Sha256Digest digest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Digest = digest ?? throw new ArgumentNullException(nameof(digest));
    }

    public string Name { get; }

    public Sha256Digest Digest { get; }

    public static VerifyProfileOutput BuiltIn (
        string name,
        Sha256Digest digest)
    {
        return new BuiltInVerifyProfileOutput(name, digest);
    }

    public static VerifyProfileOutput FromFile (
        string name,
        string path,
        Sha256Digest digest)
    {
        return new FileVerifyProfileOutput(name, path, digest);
    }
}

/// <summary> Represents a verify profile supplied by uCLI itself. </summary>
internal sealed record BuiltInVerifyProfileOutput : VerifyProfileOutput
{
    public BuiltInVerifyProfileOutput (
        string name,
        Sha256Digest digest)
        : base(name, digest)
    {
    }
}

/// <summary> Represents a verify profile supplied by a repository file. </summary>
internal sealed record FileVerifyProfileOutput : VerifyProfileOutput
{
    public FileVerifyProfileOutput (
        string name,
        string path,
        Sha256Digest digest)
        : base(name, digest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (char.IsWhiteSpace(path[0]) || char.IsWhiteSpace(path[^1]))
        {
            throw new ArgumentException(
                "Verify profile path must not have leading or trailing whitespace.",
                nameof(path));
        }

        Path = path;
    }

    public string Path { get; }
}
