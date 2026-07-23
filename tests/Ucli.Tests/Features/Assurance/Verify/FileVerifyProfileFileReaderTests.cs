using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Features.Assurance.Verify;
using Xunit.Sdk;

namespace MackySoft.Ucli.Tests.Features.Assurance.Verify;

public sealed class FileVerifyProfileFileReaderTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadAsync_WithRepositoryRelativeProfilePath_ReturnsJsonAndRelativePath ()
    {
        using var repository = TestDirectories.CreateTempScope("ucli-verify", nameof(ReadAsync_WithRepositoryRelativeProfilePath_ReturnsJsonAndRelativePath));
        repository.WriteFile("profiles/verify.json", """{"schemaVersion":1,"steps":[]}""");
        var reader = new FileVerifyProfileFileReader();

        var result = await reader.ReadAsync("profiles/verify.json", AbsolutePath.Parse(repository.FullPath));

        Assert.True(result.IsSuccess);
        Assert.Equal("""{"schemaVersion":1,"steps":[]}""", result.Json);
        Assert.Equal("profiles/verify.json", result.RepositoryRelativePath);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadAsync_WithProfilePathOutsideRepository_ReturnsInvalidArgument ()
    {
        using var repository = TestDirectories.CreateTempScope("ucli-verify", nameof(ReadAsync_WithProfilePathOutsideRepository_ReturnsInvalidArgument));
        using var outside = TestDirectories.CreateTempScope("ucli-verify", "outside-profile");
        var path = outside.WriteFile("verify.json", """{"schemaVersion":1,"steps":[]}""");
        var reader = new FileVerifyProfileFileReader();

        var result = await reader.ReadAsync(path, AbsolutePath.Parse(repository.FullPath));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadAsync_WithProfileFileSymbolicLink_ReturnsInvalidArgument ()
    {
        using var repository = TestDirectories.CreateTempScope("ucli-verify", nameof(ReadAsync_WithProfileFileSymbolicLink_ReturnsInvalidArgument));
        using var outside = TestDirectories.CreateTempScope("ucli-verify", "outside-profile-link-target");
        var targetPath = outside.WriteFile("verify.json", """{"schemaVersion":1,"steps":[]}""");
        var profilePath = Path.Combine(repository.FullPath, "verify.json");
        if (!TestSymbolicLinks.TryCreateFile(profilePath, targetPath))
        {
            throw SkipException.ForSkip("Symbolic links are not supported by this test environment.");
        }

        var reader = new FileVerifyProfileFileReader();

        var result = await reader.ReadAsync("verify.json", AbsolutePath.Parse(repository.FullPath));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadAsync_WithProfileDirectorySymbolicLink_ReturnsInvalidArgument ()
    {
        using var repository = TestDirectories.CreateTempScope("ucli-verify", nameof(ReadAsync_WithProfileDirectorySymbolicLink_ReturnsInvalidArgument));
        using var outside = TestDirectories.CreateTempScope("ucli-verify", "outside-profile-directory-link-target");
        outside.WriteFile("verify.json", """{"schemaVersion":1,"steps":[]}""");
        var linkPath = Path.Combine(repository.FullPath, "linked");
        if (!TestSymbolicLinks.TryCreateDirectory(linkPath, outside.FullPath))
        {
            throw SkipException.ForSkip("Symbolic links are not supported by this test environment.");
        }

        var reader = new FileVerifyProfileFileReader();

        var result = await reader.ReadAsync("linked/verify.json", AbsolutePath.Parse(repository.FullPath));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadAsync_OnUnixWithLiteralBackslashProfileName_ReturnsInvalidArgument ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var repository = TestDirectories.CreateTempScope(
            "ucli-verify",
            nameof(ReadAsync_OnUnixWithLiteralBackslashProfileName_ReturnsInvalidArgument));
        repository.WriteFile(@"profiles/verify\local.json", """{"schemaVersion":1,"steps":[]}""");
        var reader = new FileVerifyProfileFileReader();

        var result = await reader.ReadAsync(
            @"profiles/verify\local.json",
            AbsolutePath.Parse(repository.FullPath));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("portable repository-relative path", result.Error.Message, StringComparison.Ordinal);
    }

}
