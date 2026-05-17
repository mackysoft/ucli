using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Features.Assurance.Verify;

namespace MackySoft.Ucli.Tests.Features.Assurance.Verify;

public sealed class FileVerifyProfileFileReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_WithProfilePathOutsideRepository_ReturnsInvalidArgument ()
    {
        using var repository = TestDirectories.CreateTempScope("ucli-verify", nameof(ReadAsync_WithProfilePathOutsideRepository_ReturnsInvalidArgument));
        using var outside = TestDirectories.CreateTempScope("ucli-verify", "outside-profile");
        var path = outside.WriteFile("verify.json", """{"schemaVersion":1,"steps":[]}""");
        var reader = new FileVerifyProfileFileReader();

        var result = await reader.ReadAsync(path, repository.FullPath);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
    }
}
