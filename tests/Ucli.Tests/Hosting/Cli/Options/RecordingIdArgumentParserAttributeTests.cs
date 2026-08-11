using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Options;

public sealed class RecordingIdArgumentParserAttributeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenValueIsCanonicalNonZeroUuid_ReturnsGuid ()
    {
        const string value = "6f9619ff-8b86-d011-b42d-00c04fc964ff";

        var parsed = RecordingIdArgumentParserAttribute.TryParse(value, out var recordingId);

        Assert.True(parsed);
        Assert.Equal(Guid.Parse(value), recordingId);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("6F9619FF-8B86-D011-B42D-00C04FC964FF ")]
    [InlineData("{6F9619FF-8B86-D011-B42D-00C04FC964FF}")]
    [Trait("Size", "Small")]
    public void TryParse_WhenValueIsNotCanonicalNonZeroUuid_ReturnsFalse (string value)
    {
        var parsed = RecordingIdArgumentParserAttribute.TryParse(value, out var recordingId);

        Assert.False(parsed);
        Assert.Equal(Guid.Empty, recordingId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParseOptional_WhenValueIsCanonicalNonZeroUuid_ReturnsGuid ()
    {
        const string value = "6f9619ff-8b86-d011-b42d-00c04fc964ff";

        var parsed = OptionalRecordingIdArgumentParserAttribute.TryParse(value, out var recordingId);

        Assert.True(parsed);
        Assert.Equal(Guid.Parse(value), recordingId);
    }
}
