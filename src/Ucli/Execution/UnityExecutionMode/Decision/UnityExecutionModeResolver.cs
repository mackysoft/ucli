namespace MackySoft.Ucli.Execution;

/// <summary> Resolves normalized Unity execution mode values from optional command input. </summary>
internal static class UnityExecutionModeResolver
{
    /// <summary> Resolves one effective Unity execution mode from optional command input. </summary>
    /// <param name="optionValue"> The optional raw <c>--mode</c> option value. </param>
    /// <returns> The mode-resolution result. </returns>
    public static UnityExecutionModeResolutionResult Resolve (string? optionValue)
    {
        if (UnityExecutionModeParser.TryParse(optionValue, out var mode))
        {
            return UnityExecutionModeResolutionResult.Success(mode);
        }

        return UnityExecutionModeResolutionResult.Failure(
            UnityExecutionModeDecisionResultFactory.CreateInvalidModeError());
    }
}