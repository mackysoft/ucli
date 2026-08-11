using MackySoft.Ucli.Application.Features.Recording.Requests;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Application.Tests.Features.Recording.Requests;

public sealed class GameViewRecordingRequestTests
{
    [Fact]
    public void ParseAndNormalize_WhenDurationIsOmitted_FixesDefaultInCanonicalDigest ()
    {
        var parsed = GameViewRecordingRequestParser.Parse("""
            {
              "schemaVersion": 1,
              "resolution": { "width": 640, "height": 480 },
              "frameRate": 30
            }
            """);

        var normalized = GameViewRecordingRequestNormalizer.Normalize(
            parsed.Request!,
            minimumWidth: 2,
            maximumWidth: 3840,
            minimumHeight: 2,
            maximumHeight: 2160,
            dimensionMultiple: 2,
            minimumFrameRate: 1,
            maximumFrameRate: 60,
            defaultMaxDurationSeconds: 10,
            maximumMaxDurationSeconds: 600);

        Assert.True(parsed.IsSuccess);
        Assert.True(normalized.IsSuccess);
        Assert.Equal(10, normalized.Request!.MaxDurationSeconds);
        Assert.Contains("\"maxDurationSeconds\":10", normalized.Request.CanonicalJson);
        Assert.Equal(
            Sha256Digest.Compute(System.Text.Encoding.UTF8.GetBytes(normalized.Request.CanonicalJson)),
            normalized.Request.Digest);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"resolution\":{\"width\":640,\"height\":480},\"frameRate\":30,\"extra\":true}")]
    [InlineData("{\"schemaVersion\":1,\"resolution\":{\"width\":640,\"height\":480},\"frameRate\":30,\"frameRate\":60}")]
    [InlineData("{\"schemaVersion\":1,\"resolution\":{\"width\":640,\"height\":480},\"frameRate\":30,\"maxDurationSeconds\":null}")]
    [InlineData("{\"schemaVersion\":1,\"resolution\":{\"width\":640,\"height\":480},\"frameRate\":30,\"maxDurationSeconds\":0}")]
    public void Parse_WhenContractIsInvalid_ReturnsInvalidArgument (string json)
    {
        var result = GameViewRecordingRequestParser.Parse(json);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.Error!.Code);
    }

    [Theory]
    [InlineData(641, 480)]
    [InlineData(640, 481)]
    public void Normalize_WhenPositiveDimensionDoesNotSatisfyAdapterMultiple_ReturnsInvalidArgument (
        int width,
        int height)
    {
        var parsed = GameViewRecordingRequestParser.Parse($$"""
            {
              "schemaVersion": 1,
              "resolution": { "width": {{width}}, "height": {{height}} },
              "frameRate": 30
            }
            """);

        Assert.True(parsed.IsSuccess);

        var normalized = GameViewRecordingRequestNormalizer.Normalize(
            parsed.Request!,
            minimumWidth: 2,
            maximumWidth: 3840,
            minimumHeight: 2,
            maximumHeight: 2160,
            dimensionMultiple: 2,
            minimumFrameRate: 1,
            maximumFrameRate: 60,
            defaultMaxDurationSeconds: 10,
            maximumMaxDurationSeconds: 600);

        Assert.False(normalized.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, normalized.Error!.Code);
    }

    [Fact]
    public void Normalize_WhenValueExceedsAdapterLimit_ReturnsInvalidArgument ()
    {
        var request = new GameViewRecordingRequestDocument(
            GameViewRecordingRequest.CurrentSchemaVersion,
            new PixelDimensions(1920, 1080),
            frameRate: 120,
            UcliOptionalInt32.FromValue(30));

        var result = GameViewRecordingRequestNormalizer.Normalize(
            request,
            2,
            3840,
            2,
            2160,
            2,
            1,
            60,
            10,
            600);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.Error!.Code);
    }
}
