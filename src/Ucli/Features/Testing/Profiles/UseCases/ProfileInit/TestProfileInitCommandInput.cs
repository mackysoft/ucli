namespace MackySoft.Ucli.Features.Testing.Profiles.UseCases.ProfileInit;

/// <summary> Represents normalized CLI input values for one test-profile initialization execution. </summary>
/// <param name="OutputPath"> The optional output path for the generated profile file. </param>
/// <param name="Force"> Whether existing profile files can be overwritten. </param>
internal sealed record TestProfileInitCommandInput (
    string? OutputPath,
    bool Force);