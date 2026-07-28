using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

public sealed class FileSystemNodeIdentityTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IsSamePhysicalNodeAs_WhenUpperIdentifierHalfDiffers_ReturnsFalse ()
    {
        var expected = new FileSystemNodeIdentity(
            VolumeOrDevice: 1,
            NodeIdentifier: new FileSystemNodeIdentifier(Low: 2, High: 3),
            LinkCount: 1,
            Classification: new FileSystemNodeClassification(
                IsRegularFile: true,
                IsDirectory: false,
                IsReparsePoint: false));
        var replacement = expected with
        {
            NodeIdentifier = new FileSystemNodeIdentifier(Low: 2, High: 4),
        };

        Assert.False(expected.IsSamePhysicalNodeAs(replacement));
    }
}
