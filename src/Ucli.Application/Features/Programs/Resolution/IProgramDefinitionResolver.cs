namespace MackySoft.Ucli.Application.Features.Programs.Resolution;

/// <summary> Resolves Program references and fixes the canonical definition identity. </summary>
internal interface IProgramDefinitionResolver
{
    /// <summary> Resolves one Program without observing Unity or creating a Program Run. </summary>
    ValueTask<ProgramDefinitionResolutionResult> ResolveAsync (
        ProgramDefinitionResolutionInput input,
        CancellationToken cancellationToken = default);
}
