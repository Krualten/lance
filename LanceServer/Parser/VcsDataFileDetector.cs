using System.Text.RegularExpressions;

namespace LanceServer.Parser;

/// <summary>
/// Recognizes SINUMERIK VCS compensation data files which may use the .SPF extension
/// despite not containing executable NC code.
/// </summary>
public static class VcsDataFileDetector
{
    private static readonly Regex SectionHeader =
        new(@"^\[[A-Z0-9_]+\](?:\s*//.*)?$", RegexOptions.IgnoreCase);

    private static readonly Regex FileTypeVcs =
        new(@"^\s*FILE_TYPE\s*=\s*VCS(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private static readonly Regex GridPointsAssignment =
        new(@"^\s*GRIDPOINTS\s*=", RegexOptions.IgnoreCase | RegexOptions.Multiline);

    public static bool IsVcsData(string code)
    {
        var firstContentLine = code
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
            .Select(line => line.Trim())
            .FirstOrDefault(line =>
                line.Length > 0
                && !line.StartsWith("//", StringComparison.Ordinal)
                && !line.StartsWith(';'));

        if (firstContentLine == null || !SectionHeader.IsMatch(firstContentLine))
        {
            return false;
        }

        return FileTypeVcs.IsMatch(code) || GridPointsAssignment.IsMatch(code);
    }
}
