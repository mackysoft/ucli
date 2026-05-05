using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Tests;

public sealed class FileTestRunProfileJsonReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadTextAsync_WithMissingProfilePath_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-profile-json-reader", "missing-profile");
        var reader = new FileTestRunProfileJsonReader();
        var missingPath = scope.GetPath("missing.profile.json");

        var result = await reader.ReadTextAsync(missingPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("profilePath does not exist", error.Message, StringComparison.Ordinal);
    }
}
