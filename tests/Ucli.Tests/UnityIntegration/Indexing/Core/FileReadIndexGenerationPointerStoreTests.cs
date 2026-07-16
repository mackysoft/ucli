using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.Tests.Index;

public sealed class FileReadIndexGenerationPointerStoreTests
{
    [Theory]
    [InlineData("")]
    [InlineData("00000000000000000000000000000000")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("01234567-89ab-cdef-0123-456789abcdef")]
    [InlineData("not-a-guid")]
    [Trait("Size", "Medium")]
    public async Task Read_WhenValueIsNotOneNonEmptyNFormatGuid_ThrowsInvalidDataException (string value)
    {
        using var scope = TestDirectories.CreateTempScope("read-index-generation-pointer", "invalid");
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        FileReadIndexArtifactReaderTestSupport.WriteText(
            UcliStoragePathResolver.ResolveReadIndexCurrentGenerationPath(scope.FullPath, fingerprint),
            value);
        var store = new FileReadIndexGenerationPointerStore();

        await Assert.ThrowsAsync<InvalidDataException>(async () => await store.ReadAsync(
            scope.FullPath,
            fingerprint,
            CancellationToken.None));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Publish_WithEmptyGuid_RejectsValueBeforeCreatingPointer ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-generation-pointer", "empty");
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var store = new FileReadIndexGenerationPointerStore();

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () => await store.PublishAsync(
            scope.FullPath,
            fingerprint,
            Guid.Empty,
            CancellationToken.None));

        Assert.Equal("generationId", exception.ParamName);
        Assert.False(File.Exists(UcliStoragePathResolver.ResolveReadIndexCurrentGenerationPath(
            scope.FullPath,
            fingerprint)));
    }
}
