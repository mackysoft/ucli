using MackySoft.Ucli.Application.Features.Programs.Resolution;

namespace MackySoft.Ucli.Application.Features.Programs.Validate;

/// <summary> Performs the offline Program validation projection. </summary>
internal interface IProgramValidationService
{
    /// <summary> Validates only Program syntax, references, canonicalization, and definition identity. </summary>
    ValueTask<ProgramDefinitionResolutionResult> ValidateAsync (
        ProgramDefinitionResolutionInput input,
        CancellationToken cancellationToken = default);
}
