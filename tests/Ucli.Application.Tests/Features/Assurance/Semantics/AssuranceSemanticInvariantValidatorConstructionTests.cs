using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics;

public sealed class AssuranceSemanticInvariantValidatorConstructionTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenPayloadRulesAreEmpty_RejectsMissingValidationDependency ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new AssuranceSemanticInvariantValidator(
            new StaticCodeCatalog([]),
            Array.Empty<IAssurancePayloadInvariantRule>(),
            [new NoOpClaimRule()]));

        Assert.Equal("payloadRules", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenClaimRulesAreEmpty_RejectsMissingValidationDependency ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new AssuranceSemanticInvariantValidator(
            new StaticCodeCatalog([]),
            [new NoOpPayloadRule()],
            Array.Empty<IAssuranceClaimInvariantRule>()));

        Assert.Equal("claimRules", exception.ParamName);
    }

    private sealed class NoOpPayloadRule : IAssurancePayloadInvariantRule
    {
        public void ValidatePayload (
            JsonElement payload,
            List<AssuranceSemanticInvariantViolation> violations)
        {
        }
    }

    private sealed class NoOpClaimRule : IAssuranceClaimInvariantRule
    {
        public void ValidateClaim (
            JsonElement payload,
            JsonElement claimElement,
            string claimPath,
            UcliCode claimId,
            List<AssuranceSemanticInvariantViolation> violations)
        {
        }
    }
}
