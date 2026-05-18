using System.Globalization;
using System.Security.Cryptography;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Artifacts;

/// <summary> Creates collision-resistant compile run identifiers. </summary>
internal sealed class CompileRunIdFactory : ICompileRunIdFactory
{
    private const string RunIdTimestampFormat = "yyyyMMdd_HHmmss'Z'";

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="CompileRunIdFactory" /> class. </summary>
    public CompileRunIdFactory (TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string Create ()
    {
        var utcNow = timeProvider.GetUtcNow();
        var suffix = RandomNumberGenerator.GetHexString(8).ToLowerInvariant();
        return $"{utcNow.ToString(RunIdTimestampFormat, CultureInfo.InvariantCulture)}_{suffix}";
    }
}
