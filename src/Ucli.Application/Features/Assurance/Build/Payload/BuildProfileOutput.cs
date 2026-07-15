using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents the build profile identity used by one build run. </summary>
internal sealed record BuildProfileOutput (
    string Path,
    Sha256Digest Digest);
