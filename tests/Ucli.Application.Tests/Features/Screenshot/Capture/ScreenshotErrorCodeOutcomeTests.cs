namespace MackySoft.Ucli.Application.Tests.Screenshot;

public sealed class ScreenshotErrorCodeOutcomeTests
{
    public static TheoryData<UcliCode, int> Cases => new()
    {
        { ScreenshotErrorCodes.ScreenshotRequestedSizeUnsupported, (int)ApplicationOutcome.InvalidArgument },
        { ScreenshotErrorCodes.ScreenshotRequiresGuiSession, (int)ApplicationOutcome.ToolError },
        { ScreenshotErrorCodes.ScreenshotCaptureUnsupported, (int)ApplicationOutcome.ToolError },
    };

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(Cases))]
    public void FromCode_UsesScreenshotFailureOutcome (
        UcliCode code,
        int expectedOutcome)
    {
        var failure = ApplicationFailure.FromCode(code, "Screenshot failure.");

        Assert.Equal((ApplicationOutcome)expectedOutcome, failure.Outcome);
    }
}
