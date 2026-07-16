using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;

/// <summary> Represents the effective verify profile identity. </summary>
internal sealed record VerifyProfileOutput
{
    public VerifyProfileOutput (
        VerifyProfileSource Source,
        string Name,
        string? Path,
        Sha256Digest Digest)
    {
        if (!ContractLiteralCodec.IsDefined(Source))
        {
            throw new ArgumentOutOfRangeException(nameof(Source), Source, "Unsupported verify profile source.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        this.Source = Source;
        this.Name = Name;
        this.Path = Path;
        this.Digest = Digest ?? throw new ArgumentNullException(nameof(Digest));
    }

    public VerifyProfileSource Source { get; }

    public string Name { get; }

    public string? Path { get; }

    public Sha256Digest Digest { get; }
}
