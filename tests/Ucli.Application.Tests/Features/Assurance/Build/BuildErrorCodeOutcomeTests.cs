namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildErrorCodeOutcomeTests
{
    public static TheoryData<UcliCode, int> BuildErrorOutcomeCases
    {
        get
        {
            var data = new TheoryData<UcliCode, int>();
            data.Add(BuildErrorCodes.BuildProfileInvalid, (int)ApplicationOutcome.InvalidArgument);
            data.Add(BuildErrorCodes.BuildTargetUnsupported, (int)ApplicationOutcome.InvalidArgument);
            data.Add(BuildErrorCodes.BuildInputsInvalid, (int)ApplicationOutcome.InvalidArgument);
            data.Add(BuildErrorCodes.BuildTargetModuleMissing, (int)ApplicationOutcome.ToolError);
            data.Add(BuildErrorCodes.BuildDirtyStatePresent, (int)ApplicationOutcome.ToolError);
            data.Add(BuildErrorCodes.BuildArtifactWriteFailed, (int)ApplicationOutcome.ToolError);
            data.Add(BuildErrorCodes.BuildOutputManifestFailed, (int)ApplicationOutcome.ToolError);
            data.Add(BuildErrorCodes.BuildOutputManifestDigestMismatch, (int)ApplicationOutcome.ToolError);
            data.Add(BuildErrorCodes.BuildOutputManifestArtifactDigestMismatch, (int)ApplicationOutcome.ToolError);
            return data;
        }
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(BuildErrorOutcomeCases))]
    public void ApplicationFailure_FromBuildErrorCode_UsesRegisteredOutcome (
        UcliCode code,
        int expectedOutcome)
    {
        var failure = ApplicationFailure.FromCode(code, "Build failure.");

        Assert.Equal(code, failure.Code);
        Assert.Equal((ApplicationOutcome)expectedOutcome, failure.Outcome);
    }
}
