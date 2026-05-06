using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Tests.Configuration;

public sealed class UcliConfigResultTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WithEmptyDiagnostics_ThrowsArgumentException ()
    {
        var diagnostics = Array.Empty<UcliConfigDiagnostic>();

        Assert.Throws<ArgumentException>(() => UcliConfigLoadResult.Failure(diagnostics));
        Assert.Throws<ArgumentException>(() => UcliConfigSaveResult.Failure(diagnostics));
        Assert.Throws<ArgumentException>(() => UcliConfigBuildResult.Failure(diagnostics));
        Assert.Throws<ArgumentException>(() => UcliConfigDocumentBuildResult.Failure(diagnostics));
        Assert.Throws<ArgumentException>(() => UcliConfigSchemaValidationResult.Failure(diagnostics));
    }
}
