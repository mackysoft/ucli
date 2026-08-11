using System.Text;
using MackySoft.Ucli.Application.Features.Programs.Resolution;
using MackySoft.Ucli.Features.Programs.Resolution;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Features.Programs.Resolution;

public sealed class FileProgramDefinitionFileReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_InternalRegularFile_ReturnsBytesAndPhysicalPath ()
    {
        using var scope = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(scope.Root, "request.json"), "{}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var root = AbsolutePath.Parse(scope.Root);

        var result = await new FileProgramDefinitionFileReader().ReadAsync(
            ContainedPath.Create(root, RootRelativePath.Parse("request.json")));

        var success = Assert.IsType<ProgramDefinitionFileReadSuccess>(result);
        Assert.Equal("{}", Encoding.UTF8.GetString(success.Content));
        Assert.Equal("request.json", Path.GetFileName(success.PhysicalPath.Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_ExternalLeafOrAncestorSymlink_ReturnsOutsideBoundary ()
    {
        using var scope = new TemporaryDirectory();
        using var external = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(external.Root, "request.json"), "{}", Encoding.UTF8);
        File.CreateSymbolicLink(Path.Combine(scope.Root, "leaf.json"), Path.Combine(external.Root, "request.json"));
        Directory.CreateSymbolicLink(Path.Combine(scope.Root, "directory"), external.Root);
        var root = AbsolutePath.Parse(scope.Root);
        var reader = new FileProgramDefinitionFileReader();

        var leaf = await reader.ReadAsync(ContainedPath.Create(root, RootRelativePath.Parse("leaf.json")));
        var ancestor = await reader.ReadAsync(ContainedPath.Create(root, RootRelativePath.Parse("directory/request.json")));

        Assert.IsType<ProgramDefinitionFileReadOutsideBoundary>(leaf);
        Assert.IsType<ProgramDefinitionFileReadOutsideBoundary>(ancestor);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_InternalLeafSymlink_ReturnsTargetBytesAndPhysicalPath ()
    {
        using var scope = new TemporaryDirectory();
        var target = Path.Combine(scope.Root, "actual-request.json");
        File.WriteAllText(target, "{\"leaf\":true}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var link = Path.Combine(scope.Root, "leaf.json");
        File.CreateSymbolicLink(link, target);
        var root = AbsolutePath.Parse(scope.Root);

        var result = await new FileProgramDefinitionFileReader().ReadAsync(
            ContainedPath.Create(root, RootRelativePath.Parse("leaf.json")));

        var success = Assert.IsType<ProgramDefinitionFileReadSuccess>(result);
        Assert.Equal("{\"leaf\":true}", Encoding.UTF8.GetString(success.Content));
        Assert.Equal("actual-request.json", Path.GetFileName(success.PhysicalPath.Value));
        AssertSamePhysicalFile(target, success.PhysicalPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_InternalAncestorDirectorySymlink_ReturnsTargetBytesAndPhysicalPath ()
    {
        using var scope = new TemporaryDirectory();
        var actualDirectory = Path.Combine(scope.Root, "actual");
        Directory.CreateDirectory(actualDirectory);
        var target = Path.Combine(actualDirectory, "request.json");
        File.WriteAllText(target, "{\"ancestor\":true}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var link = Path.Combine(scope.Root, "directory");
        Directory.CreateSymbolicLink(link, actualDirectory);
        var root = AbsolutePath.Parse(scope.Root);

        var result = await new FileProgramDefinitionFileReader().ReadAsync(
            ContainedPath.Create(root, RootRelativePath.Parse("directory/request.json")));

        var success = Assert.IsType<ProgramDefinitionFileReadSuccess>(result);
        Assert.Equal("{\"ancestor\":true}", Encoding.UTF8.GetString(success.Content));
        Assert.Equal("request.json", Path.GetFileName(success.PhysicalPath.Value));
        AssertSamePhysicalFile(target, success.PhysicalPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_NonRegularFile_ReturnsUnavailable ()
    {
        using var scope = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(scope.Root, "directory"));
        var root = AbsolutePath.Parse(scope.Root);

        var result = await new FileProgramDefinitionFileReader().ReadAsync(
            ContainedPath.Create(root, RootRelativePath.Parse("directory")));

        Assert.IsType<ProgramDefinitionFileReadUnavailable>(result);
    }

    private static void AssertSamePhysicalFile (string expectedPath, AbsolutePath actualPath)
    {
        var expected = FileSystemNodeIdentityReader.ReadPath(AbsolutePath.Parse(expectedPath), "expected Program definition file");
        var actual = FileSystemNodeIdentityReader.ReadPath(actualPath, "returned Program definition file");
        Assert.True(expected.IsSamePhysicalNodeAs(actual));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory ()
        {
            Root = Path.Combine(Path.GetTempPath(), $"ucli-program-reader-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose ()
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
