namespace MackySoft.Tests;

internal sealed class CurrentDirectoryScope : IDisposable
{
    private readonly string originalCurrentDirectory;

    private bool disposed;

    public CurrentDirectoryScope (string currentDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectoryPath);

        originalCurrentDirectory = Environment.CurrentDirectory;
        Environment.CurrentDirectory = currentDirectoryPath;
    }

    public void Dispose ()
    {
        if (disposed)
        {
            return;
        }

        Environment.CurrentDirectory = originalCurrentDirectory;
        disposed = true;
    }
}
