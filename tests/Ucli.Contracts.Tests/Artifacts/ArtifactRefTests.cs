using System.Text.Json;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Artifacts;

public sealed class ArtifactRefTests
{
    private static readonly DateTimeOffset PublicationTime =
        new(2026, 7, 28, 12, 34, 56, TimeSpan.Zero);

    [Fact]
    [Trait("Size", "Small")]
    public void PathReference_RoundTripsAsThePublicArtifactContract ()
    {
        ArtifactRef reference = new PathArtifactRef(
            new ArtifactKind(TextVocabulary.GetText(ScreenshotArtifactKind.Screenshot)),
            new ArtifactMediaType(TextVocabulary.GetText(ScreenshotArtifactMediaType.Png)),
            new ArtifactPath(".ucli/local/screenshots/capture.png"),
            Sha256Digest.Parse(new string('a', 64)),
            sizeBytes: 42,
            PublicationTime);

        var json = JsonSerializer.Serialize(reference, IpcJsonSerializerOptions.StrictPropertyNames);
        var roundTripped = JsonSerializer.Deserialize<ArtifactRef>(
            json,
            IpcJsonSerializerOptions.StrictPropertyNames);

        Assert.NotNull(roundTripped);
        Assert.Equal(reference, roundTripped);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            TextVocabulary.GetText(ArtifactLocationKind.Path),
            document.RootElement.GetProperty("locationKind").GetString());
        Assert.Equal(
            TextVocabulary.GetText(ScreenshotArtifactKind.Screenshot),
            document.RootElement.GetProperty("kind").GetString());
        Assert.Equal(
            TextVocabulary.GetText(ScreenshotArtifactMediaType.Png),
            document.RootElement.GetProperty("mediaType").GetString());
        Assert.Equal(
            ".ucli/local/screenshots/capture.png",
            document.RootElement.GetProperty("path").GetString());
        Assert.False(document.RootElement.TryGetProperty("uri", out _));
        Assert.Equal(42UL, document.RootElement.GetProperty("sizeBytes").GetUInt64());
        Assert.Equal(
            "2026-07-28T12:34:56.0000000Z",
            document.RootElement.GetProperty("createdAtUtc").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UriReference_RoundTripsAsThePublicArtifactContract ()
    {
        ArtifactRef reference = new UriArtifactRef(
            new ArtifactKind("report"),
            new ArtifactMediaType("application/json"),
            new ArtifactUri("https://artifacts.example.test/reports/report%20file.json"),
            Sha256Digest.Parse(new string('b', 64)),
            sizeBytes: 84,
            PublicationTime);

        var json = JsonSerializer.Serialize(reference, IpcJsonSerializerOptions.StrictPropertyNames);
        var roundTripped = JsonSerializer.Deserialize<ArtifactRef>(
            json,
            IpcJsonSerializerOptions.StrictPropertyNames);

        Assert.NotNull(roundTripped);
        Assert.Equal(reference, roundTripped);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            TextVocabulary.GetText(ArtifactLocationKind.Uri),
            document.RootElement.GetProperty("locationKind").GetString());
        Assert.False(document.RootElement.TryGetProperty("path", out _));
        Assert.Equal(
            "https://artifacts.example.test/reports/report%20file.json",
            document.RootElement.GetProperty("uri").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PathAndUriReference_RoundTripsAsThePublicArtifactContract ()
    {
        ArtifactRef reference = new PathAndUriArtifactRef(
            new ArtifactKind("editorLog"),
            new ArtifactMediaType("text/plain; charset=utf-8"),
            new ArtifactPath(".ucli/local/test-runs/editor.log"),
            new ArtifactUri("https://artifacts.example.test/test-runs/editor.log"),
            Sha256Digest.Parse(new string('c', 64)),
            sizeBytes: 126,
            PublicationTime);

        var json = JsonSerializer.Serialize(reference, IpcJsonSerializerOptions.StrictPropertyNames);
        var roundTripped = JsonSerializer.Deserialize<ArtifactRef>(
            json,
            IpcJsonSerializerOptions.StrictPropertyNames);

        Assert.Equal(reference, roundTripped);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            TextVocabulary.GetText(ArtifactLocationKind.PathAndUri),
            document.RootElement.GetProperty("locationKind").GetString());
        Assert.Equal(
            ".ucli/local/test-runs/editor.log",
            document.RootElement.GetProperty("path").GetString());
        Assert.Equal(
            "https://artifacts.example.test/test-runs/editor.log",
            document.RootElement.GetProperty("uri").GetString());
        Assert.Equal(
            "text/plain; charset=utf-8",
            document.RootElement.GetProperty("mediaType").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ArtifactReference_RejectsANonUtcPublicationTime ()
    {
        var kind = new ArtifactKind("report");
        var mediaType = new ArtifactMediaType("application/json");
        var digest = Sha256Digest.Parse(new string('b', 64));

        Assert.Throws<ArgumentException>(
            () => new PathArtifactRef(
                kind,
                mediaType,
                new ArtifactPath("report.json"),
                digest,
                sizeBytes: 0,
                PublicationTime.ToOffset(TimeSpan.FromHours(9))));
    }

    [Theory]
    [InlineData("relative/report.json")]
    [InlineData("https://artifacts.example.test/report name.json")]
    [InlineData("https://artifacts.example.test/report%2.json")]
    [InlineData("https://artifacts.example.test/report#fragment")]
    [Trait("Size", "Small")]
    public void ArtifactUri_RejectsTextOutsideItsAbsoluteLexicalContract (string value)
    {
        Assert.Throws<ArgumentException>(() => new ArtifactUri(value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ArtifactPath_RejectsNonCanonicalOrEscapingPaths ()
    {
        Assert.Throws<ArgumentException>(() => new ArtifactPath("../capture.png"));
        Assert.Throws<ArgumentException>(() => new ArtifactPath("captures\\capture.png"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ArtifactMediaType_UsesCanonicalTokenValuedParameters ()
    {
        var mediaType = new ArtifactMediaType("text/plain; charset=UTF-8");

        Assert.Equal("text/plain; charset=UTF-8", mediaType.Value);
        Assert.Throws<ArgumentException>(
            () => new ArtifactMediaType("text/plain;charset=utf-8"));
        Assert.Throws<ArgumentException>(
            () => new ArtifactMediaType("text/plain; Charset=utf-8"));
    }
}
