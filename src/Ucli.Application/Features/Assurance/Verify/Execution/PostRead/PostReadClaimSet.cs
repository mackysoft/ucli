using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Execution.PostRead;

/// <summary> Represents post-read claims and payload-level residual risks produced from one <c>--from</c> input. </summary>
internal sealed record PostReadClaimSet (
    IReadOnlyList<VerifyClaimOutput> Claims,
    IReadOnlyList<VerifyResidualRiskOutput> ResidualRisks);
