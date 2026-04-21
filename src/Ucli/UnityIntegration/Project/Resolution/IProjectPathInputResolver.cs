namespace MackySoft.Ucli.UnityIntegration.Project.Resolution;

/// <summary> Resolves the effective Unity project path input from command options, environment variables, and fallbacks. </summary>
internal interface IProjectPathInputResolver
{
    /// <summary> Resolves one effective project path candidate. </summary>
    /// <param name="commandOptionProjectPath"> The optional <c>--projectPath</c> value. </param>
    /// <param name="fallbackProjectPath"> The optional fallback project path value used when command and environment inputs are not provided. </param>
    /// <returns> The resolved path candidate when available; otherwise <see langword="null" />. </returns>
    string? Resolve (
        string? commandOptionProjectPath,
        string? fallbackProjectPath = null);
}