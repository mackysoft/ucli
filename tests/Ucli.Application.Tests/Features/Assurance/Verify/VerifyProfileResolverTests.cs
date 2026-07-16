using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Verify;

public sealed class VerifyProfileResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithUnknownBuiltInProfile_ReturnsInvalidArgument ()
    {
        var result = VerifyProfileResolver.Resolve("built-in:external");

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
    }

    [Theory]
    [InlineData("""{"schemaVersion":1,"steps":[{"kind":"shell","required":true}]}""")]
    [InlineData("""{"schemaVersion":1,"steps":[{"kind":"compile","required":true,"command":"dotnet test"}]}""")]
    [Trait("Size", "Small")]
    public void Resolve_WithUnknownOrExternalStep_ReturnsInvalidArgument (string json)
    {
        var result = VerifyProfileResolver.ResolveFileProfileJson(json, "verify.json");

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithEffectsMismatch_ReturnsInvalidArgument ()
    {
        var result = VerifyProfileResolver.ResolveFileProfileJson(
            """
            {
              "schemaVersion": 1,
              "steps": [
                {
                  "kind": "compile",
                  "required": true,
                  "effects": []
                }
              ]
            }
            """,
            "verify.json");

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_BuiltInProfiles_ProduceStableAndDistinctDigests ()
    {
        var firstDefault = VerifyProfileResolver.Resolve("built-in:default").Profile!;
        var secondDefault = VerifyProfileResolver.Resolve("built-in:default").Profile!;
        var project = VerifyProfileResolver.Resolve("built-in:project").Profile!;

        var firstDigest = VerifyProfileDigestCalculator.Calculate(firstDefault);
        var secondDigest = VerifyProfileDigestCalculator.Calculate(secondDefault);
        var projectDigest = VerifyProfileDigestCalculator.Calculate(project);

        Assert.Equal(firstDigest, secondDigest);
        Assert.Matches("^[0-9a-f]{64}$", firstDigest.ToString());
        Assert.Matches("^[0-9a-f]{64}$", projectDigest.ToString());
        Assert.NotEqual(firstDigest, projectDigest);
        Assert.Equal(
            firstDefault.Steps.Select(static step => step.Kind),
            project.Steps.Select(static step => step.Kind));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_BuiltInMutation_DoesNotIncludeCompileEffects ()
    {
        var profile = VerifyProfileResolver.Resolve("built-in:mutation").Profile!;

        Assert.DoesNotContain(profile.Steps, static step => step.Kind == VerifyStepKind.Compile);
        Assert.DoesNotContain(
            profile.Steps.SelectMany(static step => step.Effects),
            static effect => AssuranceEffectSets.Compile.Contains(effect));
    }

    [Theory]
    [InlineData("built-in:project")]
    [InlineData("built-in:script")]
    [Trait("Size", "Small")]
    public void Resolve_CompileFocusedBuiltIns_ReturnCompileVerifierSurface (string profileName)
    {
        var profile = VerifyProfileResolver.Resolve(profileName).Profile!;

        var compileStep = Assert.Single(profile.Steps, static step => step.Kind == VerifyStepKind.Compile);
        Assert.Equal(AssuranceEffectSets.Compile, compileStep.Effects);
        Assert.True(compileStep.Required);
    }
}
