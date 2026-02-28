using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.TestProfile;

namespace MackySoft.Ucli.Tests;

public sealed class TestProfileInitServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithOutputPathWithoutJson_AppendsJsonExtensionAndWritesTemplate ()
    {
        using var scope = TestDirectories.CreateTempScope("test-profile-init-service", "append-json-extension");
        var outputPath = scope.GetPath(Path.Combine("profiles", "test-profile"));
        var expectedProfilePath = outputPath + ".json";
        var service = new TestProfileInitService();

        var result = await service.Execute(outputPath, force: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        var output = Assert.IsType<TestProfileInitExecutionOutput>(result.Output);
        FileSystemAssert.ForPath(output.ProfilePath)
            .IsRooted()
            .EqualsNormalized(expectedProfilePath);
        FileSystemAssert.ForFile(expectedProfilePath).Exists();
        AssertProfileTemplate(expectedProfilePath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithOutputPathAlreadyJson_WritesTemplateWithoutAddingExtension ()
    {
        using var scope = TestDirectories.CreateTempScope("test-profile-init-service", "preserve-json-extension");
        var outputPath = scope.GetPath(Path.Combine("profiles", "test-profile.json"));
        var service = new TestProfileInitService();

        var result = await service.Execute(outputPath, force: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<TestProfileInitExecutionOutput>(result.Output);
        FileSystemAssert.ForPath(output.ProfilePath)
            .EqualsNormalized(outputPath);
        FileSystemAssert.ForFile(outputPath).Exists();
        AssertProfileTemplate(outputPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithCustomExtension_AppendsJsonExtension ()
    {
        using var scope = TestDirectories.CreateTempScope("test-profile-init-service", "append-json-to-custom-extension");
        var outputPath = scope.GetPath(Path.Combine("profiles", "test-profile.txt"));
        var expectedProfilePath = outputPath + ".json";
        var service = new TestProfileInitService();

        var result = await service.Execute(outputPath, force: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<TestProfileInitExecutionOutput>(result.Output);
        FileSystemAssert.ForPath(output.ProfilePath)
            .EqualsNormalized(expectedProfilePath);
        FileSystemAssert.ForFile(expectedProfilePath).Exists();
        AssertProfileTemplate(expectedProfilePath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithoutForce_WhenTargetFileExists_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("test-profile-init-service", "existing-file-without-force");
        var outputPath = scope.WriteFile("test.profile.json", "{\"legacy\":true}");
        var service = new TestProfileInitService();

        var result = await service.Execute(outputPath, force: false, CancellationToken.None);

        AssertInvalidArgumentError(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithForce_WhenTargetFileExists_OverwritesTemplate ()
    {
        using var scope = TestDirectories.CreateTempScope("test-profile-init-service", "existing-file-with-force");
        var outputPath = scope.WriteFile("test.profile.json", "{\"legacy\":true}");
        var service = new TestProfileInitService();

        var result = await service.Execute(outputPath, force: true, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<TestProfileInitExecutionOutput>(result.Output);
        FileSystemAssert.ForPath(output.ProfilePath).EqualsNormalized(outputPath);
        AssertProfileTemplate(outputPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenOutputPathIsDirectory_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("test-profile-init-service", "directory-path");
        var directoryPath = scope.CreateDirectory("existing-directory.json");
        var service = new TestProfileInitService();

        var result = await service.Execute(directoryPath, force: false, CancellationToken.None);

        AssertInvalidArgumentError(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("/")]
    [InlineData("\\")]
    public async Task Execute_WhenOutputPathUsesDirectoryStyleSuffix_ReturnsInvalidArgument (string suffix)
    {
        using var scope = TestDirectories.CreateTempScope("test-profile-init-service", "directory-style-suffix");
        var filePathBase = scope.GetPath(Path.Combine("profiles", "target"));
        var directoryStylePath = filePathBase + suffix;
        var service = new TestProfileInitService();

        var result = await service.Execute(directoryStylePath, force: false, CancellationToken.None);

        AssertInvalidArgumentError(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenParentDirectoryDoesNotExist_CreatesDirectoryAndWritesTemplate ()
    {
        using var scope = TestDirectories.CreateTempScope("test-profile-init-service", "parent-directory-not-found");
        var outputPath = scope.GetPath(Path.Combine("profiles", "missing-parent-profile.json"));
        var service = new TestProfileInitService();

        var result = await service.Execute(outputPath, force: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<TestProfileInitExecutionOutput>(result.Output);
        FileSystemAssert.ForPath(output.ProfilePath).EqualsNormalized(outputPath);
        FileSystemAssert.ForDirectory(Path.GetDirectoryName(outputPath)!).Exists();
        FileSystemAssert.ForFile(outputPath).Exists();
        AssertProfileTemplate(outputPath);
    }

    private static ExecutionError AssertInvalidArgumentError (TestProfileInitExecutionResult result)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        return error;
    }

    private static void AssertProfileTemplate (string profilePath)
    {
        using var profileJson = JsonDocument.Parse(File.ReadAllText(profilePath));
        JsonAssert.For(profileJson.RootElement)
            .HasInt32("schemaVersion", UcliContractConstants.TestProfile.SchemaVersion)
            .HasString("projectPath", UcliContractConstants.TestProfile.ProjectPath)
            .IsNull("unityVersion")
            .IsNull("unityEditorPath")
            .HasString("testPlatform", UcliContractConstants.TestProfile.TestPlatformEditMode)
            .IsNull("buildTarget")
            .IsNull("testFilter")
            .HasArrayLength("testCategories", 0)
            .HasArrayLength("assemblyNames", 0)
            .IsNull("testSettingsPath")
            .HasString("outputDir", UcliContractConstants.TestProfile.OutputDir)
            .HasInt32("timeoutSeconds", UcliContractConstants.TestProfile.TimeoutSeconds);
    }
}