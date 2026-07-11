using System.Diagnostics;
using MackySoft.Tests;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests;

public sealed class ProcessOwnedTemporaryFilePathTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_ReturnsPathOwnedByCurrentProcess ()
    {
        using var scope = TestDirectories.CreateTempScope("process-owned-temporary-file", "create");
        var destinationPath = scope.GetPath("editor.log");
        using var process = Process.GetCurrentProcess();

        var temporaryPath = ProcessOwnedTemporaryFilePath.Create(destinationPath);

        Assert.True(ProcessOwnedTemporaryFilePath.TryGetOwnerProcessId(
            destinationPath,
            temporaryPath,
            out var processId));
        Assert.Equal(process.Id, processId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryGetOwnerProcessId_WithMalformedCandidate_ReturnsFalse ()
    {
        using var scope = TestDirectories.CreateTempScope("process-owned-temporary-file", "malformed");
        var destinationPath = scope.GetPath("editor.log");
        var guid = Guid.NewGuid().ToString("N");
        var candidates = new[]
        {
            destinationPath + ".tmp." + guid,
            destinationPath + ".tmp.not-a-pid." + guid,
            destinationPath + ".tmp.123.not-a-guid",
            destinationPath + ".tmp.123." + guid + ".extra",
            scope.GetPath("other.log.tmp.123." + guid),
            scope.GetPath("other/editor.log.tmp.123." + guid),
        };

        foreach (var candidate in candidates)
        {
            Assert.False(ProcessOwnedTemporaryFilePath.TryGetOwnerProcessId(
                destinationPath,
                candidate,
                out _));
        }
    }
}
