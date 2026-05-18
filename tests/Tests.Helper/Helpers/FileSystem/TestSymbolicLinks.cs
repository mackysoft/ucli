namespace MackySoft.Tests;

internal static class TestSymbolicLinks
{
    internal static bool TryCreateFile (
        string symbolicLinkPath,
        string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(symbolicLinkPath, targetPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    internal static bool TryCreateDirectory (
        string symbolicLinkPath,
        string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(symbolicLinkPath, targetPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}
