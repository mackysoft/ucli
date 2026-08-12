using MackySoft.Ucli.Application.Features.Programs.Resolution;

namespace MackySoft.Ucli.Application.Features.Programs.Validate;

/// <summary> Projects Program validation directly to definition resolution without runtime observation. </summary>
internal sealed class ProgramValidationService : IProgramValidationService
{
    private readonly IProgramDefinitionResolver definitionResolver;

    /// <summary> Initializes the validation service. </summary>
    public ProgramValidationService (IProgramDefinitionResolver definitionResolver)
    {
        this.definitionResolver = definitionResolver ?? throw new ArgumentNullException(nameof(definitionResolver));
    }

    public ValueTask<ProgramDefinitionResolutionResult> ValidateAsync (
        ProgramDefinitionResolutionInput input,
        CancellationToken cancellationToken = default)
    {
        return definitionResolver.ResolveAsync(input, cancellationToken);
    }
}
