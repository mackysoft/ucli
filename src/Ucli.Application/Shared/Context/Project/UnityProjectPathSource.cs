namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary> Identifies where the UnityProject path was obtained during resolution. </summary>
internal enum UnityProjectPathSource
{
    /// <summary> The resolver used the current working directory. </summary>
    CurrentDirectory = 0,

    /// <summary> The resolver used the explicit <c>--projectPath</c> option value. </summary>
    CommandOption = 1,
}
