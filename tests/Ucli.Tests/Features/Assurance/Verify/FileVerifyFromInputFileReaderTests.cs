using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Features.Assurance.Verify;
using Xunit.Sdk;

namespace MackySoft.Ucli.Tests.Features.Assurance.Verify;

public sealed class FileVerifyFromInputFileReaderTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadAsync_WithRepositoryRelativeFromPath_ReturnsJson ()
    {
        using var repository = TestDirectories.CreateTempScope("ucli-verify", nameof(ReadAsync_WithRepositoryRelativeFromPath_ReturnsJson));
        repository.WriteFile("artifacts/from.json", """{"protocolVersion":1}""");
        var reader = new FileVerifyFromInputFileReader();

        var result = await reader.ReadAsync("artifacts/from.json", AbsolutePath.Parse(repository.FullPath));

        Assert.True(result.IsSuccess);
        Assert.Equal("""{"protocolVersion":1}""", result.Json);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadAsync_WithFromPathOutsideRepository_ReturnsInvalidInput ()
    {
        using var repository = TestDirectories.CreateTempScope("ucli-verify", nameof(ReadAsync_WithFromPathOutsideRepository_ReturnsInvalidInput));
        using var outside = TestDirectories.CreateTempScope("ucli-verify", "outside-from");
        var path = outside.WriteFile("from.json", """{"protocolVersion":1}""");
        var reader = new FileVerifyFromInputFileReader();

        var result = await reader.ReadAsync(path, AbsolutePath.Parse(repository.FullPath));

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationFailureKind.InvalidInput, result.Error!.Kind);
        Assert.Equal(VerifyErrorCodes.VerifyInputPayloadInvalid, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadAsync_WithFromFileSymbolicLink_ReturnsInvalidInput ()
    {
        using var repository = TestDirectories.CreateTempScope("ucli-verify", nameof(ReadAsync_WithFromFileSymbolicLink_ReturnsInvalidInput));
        using var outside = TestDirectories.CreateTempScope("ucli-verify", "outside-from-link-target");
        var targetPath = outside.WriteFile("from.json", """{"protocolVersion":1}""");
        var fromPath = Path.Combine(repository.FullPath, "from.json");
        if (!TestSymbolicLinks.TryCreateFile(fromPath, targetPath))
        {
            throw SkipException.ForSkip("Symbolic links are not supported by this test environment.");
        }

        var reader = new FileVerifyFromInputFileReader();

        var result = await reader.ReadAsync("from.json", AbsolutePath.Parse(repository.FullPath));

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationFailureKind.InvalidInput, result.Error!.Kind);
        Assert.Equal(VerifyErrorCodes.VerifyInputPayloadInvalid, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadAsync_WithFromDirectorySymbolicLink_ReturnsInvalidInput ()
    {
        using var repository = TestDirectories.CreateTempScope("ucli-verify", nameof(ReadAsync_WithFromDirectorySymbolicLink_ReturnsInvalidInput));
        using var outside = TestDirectories.CreateTempScope("ucli-verify", "outside-from-directory-link-target");
        outside.WriteFile("from.json", """{"protocolVersion":1}""");
        var linkPath = Path.Combine(repository.FullPath, "linked");
        if (!TestSymbolicLinks.TryCreateDirectory(linkPath, outside.FullPath))
        {
            throw SkipException.ForSkip("Symbolic links are not supported by this test environment.");
        }

        var reader = new FileVerifyFromInputFileReader();

        var result = await reader.ReadAsync("linked/from.json", AbsolutePath.Parse(repository.FullPath));

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationFailureKind.InvalidInput, result.Error!.Kind);
        Assert.Equal(VerifyErrorCodes.VerifyInputPayloadInvalid, result.Error.Code);
    }

}
