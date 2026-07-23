namespace MackySoft.Ucli.ScreenshotFidelityOracle.Windows;

internal sealed class OracleFailureException : Exception
{
    internal OracleFailureException (string message)
        : base(message)
    {
    }
}
