namespace MackySoft.Ucli.Tests;

internal static class JsonGoldenFileAssertPathNormalizationTestSupport
{
    public static string ReplaceWithAlternateDirectorySeparators (string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, GetAlternateDirectorySeparator());
    }

    public static string ReplaceWithMixedDirectorySeparators (string path)
    {
        var result = path.ToCharArray();
        var useAlternate = false;
        for (var i = 0; i < result.Length; i++)
        {
            if (result[i] is not ('/' or '\\'))
            {
                continue;
            }

            result[i] = useAlternate
                ? GetAlternateDirectorySeparator()
                : Path.DirectorySeparatorChar;
            useAlternate = !useAlternate;
        }

        return new string(result);
    }

    public static char GetAlternateDirectorySeparator ()
    {
        return Path.DirectorySeparatorChar == '/'
            ? '\\'
            : '/';
    }
}
