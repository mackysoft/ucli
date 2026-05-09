using System.Text.RegularExpressions;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Projection;

/// <summary> Represents compiled <c>ops list</c> filters. </summary>
internal sealed record OpsListFilter (
    Regex? NameRegex,
    UcliOperationKind? Kind,
    OperationPolicy? MaxPolicy)
{
    /// <summary> Creates a compiled filter from normalized command input. </summary>
    public static bool TryCreate (
        OpsCommandInput input,
        out OpsListFilter? filter,
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(input);

        Regex? regex = null;
        if (input.NameRegex != null)
        {
            if (string.IsNullOrWhiteSpace(input.NameRegex))
            {
                filter = null;
                errorMessage = "nameRegex must not be empty.";
                return false;
            }

            if (!RegexPatternUtilities.TryCompilePattern(input.NameRegex, out regex, out var regexError))
            {
                filter = null;
                errorMessage = $"nameRegex is invalid. {regexError}";
                return false;
            }
        }

        filter = new OpsListFilter(regex, input.Kind, input.MaxPolicy);
        errorMessage = null;
        return true;
    }
}
