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
        using var scope = TestDirectories.CreateTempScope("program-definition-reader", "internal-file");
        File.WriteAllText(Path.Combine(scope.FullPath, "request.json"), "{}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var root = AbsolutePath.Parse(scope.FullPath);

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
        using var scope = TestDirectories.CreateTempScope("program-definition-reader", "external-link");
        using var external = TestDirectories.CreateTempScope("program-definition-reader", "external-target");
        File.WriteAllText(Path.Combine(external.FullPath, "request.json"), "{}", Encoding.UTF8);
        File.CreateSymbolicLink(Path.Combine(scope.FullPath, "leaf.json"), Path.Combine(external.FullPath, "request.json"));
        Directory.CreateSymbolicLink(Path.Combine(scope.FullPath, "directory"), external.FullPath);
        var root = AbsolutePath.Parse(scope.FullPath);
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
        using var scope = TestDirectories.CreateTempScope("program-definition-reader", "internal-leaf-link");
        var target = Path.Combine(scope.FullPath, "actual-request.json");
        File.WriteAllText(target, "{\"leaf\":true}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var link = Path.Combine(scope.FullPath, "leaf.json");
        File.CreateSymbolicLink(link, target);
        var root = AbsolutePath.Parse(scope.FullPath);

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
        using var scope = TestDirectories.CreateTempScope("program-definition-reader", "internal-ancestor-link");
        var actualDirectory = Path.Combine(scope.FullPath, "actual");
        Directory.CreateDirectory(actualDirectory);
        var target = Path.Combine(actualDirectory, "request.json");
        File.WriteAllText(target, "{\"ancestor\":true}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var link = Path.Combine(scope.FullPath, "directory");
        Directory.CreateSymbolicLink(link, actualDirectory);
        var root = AbsolutePath.Parse(scope.FullPath);

        var result = await new FileProgramDefinitionFileReader().ReadAsync(
            ContainedPath.Create(root, RootRelativePath.Parse("directory/request.json")));

        var success = Assert.IsType<ProgramDefinitionFileReadSuccess>(result);
        Assert.Equal("{\"ancestor\":true}", Encoding.UTF8.GetString(success.Content));
        Assert.Equal("request.json", Path.GetFileName(success.PhysicalPath.Value));
        Assert.Equal("actual", Path.GetFileName(Path.GetDirectoryName(success.PhysicalPath.Value)));
        AssertSamePhysicalFile(target, success.PhysicalPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_NonRegularFile_ReturnsUnavailable ()
    {
        using var scope = TestDirectories.CreateTempScope("program-definition-reader", "non-regular-file");
        Directory.CreateDirectory(Path.Combine(scope.FullPath, "directory"));
        var root = AbsolutePath.Parse(scope.FullPath);

        var result = await new FileProgramDefinitionFileReader().ReadAsync(
            ContainedPath.Create(root, RootRelativePath.Parse("directory")));

        Assert.IsType<ProgramDefinitionFileReadUnavailable>(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_MissingBrokenOrCyclicLink_ReturnsUnavailable ()
    {
        using var scope = TestDirectories.CreateTempScope("program-definition-reader", "unavailable-paths");
        File.CreateSymbolicLink(Path.Combine(scope.FullPath, "broken.json"), Path.Combine(scope.FullPath, "missing-target.json"));
        File.CreateSymbolicLink(Path.Combine(scope.FullPath, "first.json"), Path.Combine(scope.FullPath, "second.json"));
        File.CreateSymbolicLink(Path.Combine(scope.FullPath, "second.json"), Path.Combine(scope.FullPath, "first.json"));
        var root = AbsolutePath.Parse(scope.FullPath);
        var reader = new FileProgramDefinitionFileReader();

        var missing = await reader.ReadAsync(ContainedPath.Create(root, RootRelativePath.Parse("missing.json")));
        var broken = await reader.ReadAsync(ContainedPath.Create(root, RootRelativePath.Parse("broken.json")));
        var cycle = await reader.ReadAsync(ContainedPath.Create(root, RootRelativePath.Parse("first.json")));

        Assert.IsType<ProgramDefinitionFileReadUnavailable>(missing);
        Assert.IsType<ProgramDefinitionFileReadUnavailable>(broken);
        Assert.IsType<ProgramDefinitionFileReadUnavailable>(cycle);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_CanceledBeforeResolution_RethrowsCancellation ()
    {
        using var scope = TestDirectories.CreateTempScope("program-definition-reader", "canceled-read");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var root = AbsolutePath.Parse(scope.FullPath);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FileProgramDefinitionFileReader().ReadAsync(
            ContainedPath.Create(root, RootRelativePath.Parse("request.json")),
            cancellation.Token).AsTask());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Open_SnapshotTargetReplacedBeforeOpen_ReturnsChangedDuringRead ()
    {
        using var scope = TestDirectories.CreateTempScope("program-definition-reader", "replace-before-open");
        var target = Path.Combine(scope.FullPath, "request.json");
        var replacement = Path.Combine(scope.FullPath, "replacement.json");
        File.WriteAllText(target, "{\"before\":true}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(replacement, "{}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var root = AbsolutePath.Parse(scope.FullPath);
        var snapshot = ProgramDefinitionPhysicalPathSnapshot.Capture(root, AbsolutePath.Resolve(root, "request.json"));

        File.Move(replacement, target, overwrite: true);

        var result = ProgramDefinitionPhysicalFileReadSession.TryOpen(snapshot, out var session);

        Assert.IsType<ProgramDefinitionFileReadChangedDuringRead>(result);
        Assert.Null(session);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadContent_PathReplacedBeforeComplete_ReturnsChangedDuringRead ()
    {
        using var scope = TestDirectories.CreateTempScope("program-definition-reader", "replace-before-read");
        var target = Path.Combine(scope.FullPath, "request.json");
        var replacement = Path.Combine(scope.FullPath, "replacement.json");
        var displaced = Path.Combine(scope.FullPath, "displaced.json");
        File.WriteAllText(target, "{\"before\":true}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(replacement, "{\"after\":true}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var root = AbsolutePath.Parse(scope.FullPath);
        var snapshot = ProgramDefinitionPhysicalPathSnapshot.Capture(root, AbsolutePath.Resolve(root, "request.json"));

        Assert.Null(ProgramDefinitionPhysicalFileReadSession.TryOpen(snapshot, out var session));
        await using var openedSession = session!;
        await openedSession.ReadContentAsync(CancellationToken.None);
        File.Move(target, displaced);
        File.Move(replacement, target);

        var result = openedSession.CompleteRead();

        Assert.IsType<ProgramDefinitionFileReadChangedDuringRead>(result);
    }

    private static void AssertSamePhysicalFile (string expectedPath, AbsolutePath actualPath)
    {
        var expected = FileSystemNodeIdentityReader.ReadPath(AbsolutePath.Parse(expectedPath), "expected Program definition file");
        var actual = FileSystemNodeIdentityReader.ReadPath(actualPath, "returned Program definition file");
        Assert.True(expected.IsSamePhysicalNodeAs(actual));
    }
}
