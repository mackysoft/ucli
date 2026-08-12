using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Maps shared compiler diagnostics to the Application diagnostic boundary. </summary>
internal static class UcliConfigContractDiagnosticMapper
{
    public static UcliConfigDiagnostic[] Map (IReadOnlyList<UcliConfigContractDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        return diagnostics
            .Select(static diagnostic => UcliConfigDiagnostic.Create(
                diagnostic.Code,
                diagnostic.PropertyPath,
                diagnostic.SourcePath,
                diagnostic.Message))
            .ToArray();
    }
}
