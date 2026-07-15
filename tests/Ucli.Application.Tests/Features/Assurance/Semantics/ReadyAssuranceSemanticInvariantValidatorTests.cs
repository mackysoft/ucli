using static MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics.AssuranceSemanticInvariantValidatorTestSupport;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics.ReadyAssuranceSemanticInvariantValidatorTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics;

public sealed class ReadyAssuranceSemanticInvariantValidatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithValidAssurancePayload_ReturnsNoViolations ()
    {
        var result = ValidateReadyPayload(CreateReadyPayload());

        Assert.True(result.IsValid);
        Assert.Empty(result.Violations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithValidReadyProbeOnlyPayload_ReturnsNoViolations ()
    {
        var result = ValidateReadyPayload(CreateReadyExecutionPayload());

        Assert.True(result.IsValid);
        Assert.Empty(result.Violations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithUnknownVerifierKind_ReturnsKindPath ()
    {
        var payload = CreateReadyPayload(verifiers: [CreateVerifier(kind: "external")]);

        var result = ValidateReadyPayload(payload);

        AssertViolationPath(result, "$.verifiers[0].kind");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithReadyClaimMissingValidity_ReturnsValidityPath ()
    {
        var result = ValidateReadyPayload(CreateReadyExecutionPayload(includeValidity: false));

        AssertViolationPath(result, "$.claims[0].validity");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithAutoOneshotReadyClaimGuaranteeingReusableSession_ReturnsGuaranteePath ()
    {
        var result = ValidateReadyPayload(CreateReadyExecutionPayload(
            validity: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["kind"] = "probeOnly",
                ["guaranteesReusableSession"] = true,
            }));

        AssertViolationPath(result, "$.claims[0].validity.guaranteesReusableSession");
    }
}
