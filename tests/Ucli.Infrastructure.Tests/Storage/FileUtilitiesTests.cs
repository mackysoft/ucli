using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

public sealed class FileUtilitiesTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteAllTextAtomically_WhenTargetExists_ReplacesExistingContents ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "atomic-write-overwrite");
        var path = Path.Combine(scope.FullPath, "daemon-diagnosis.json");
        await File.WriteAllTextAsync(path, "old-contents", CancellationToken.None);

        await FileUtilities.WriteAllTextAtomically(path, "new-contents", CancellationToken.None);

        var contents = await File.ReadAllTextAsync(path, CancellationToken.None);
        Assert.Equal("new-contents", contents);

        var files = Directory.GetFiles(scope.FullPath);
        Assert.Single(files);
        Assert.Equal(Path.GetFullPath(path), Path.GetFullPath(files[0]));
    }
}
