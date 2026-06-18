using LanceServer.Core.Symbol;

namespace LanceServer.Core.Workspace;

/// <summary>
/// Orders procedure candidates according to the standard SINUMERIK subprogram search path.
/// The document containing the reference is used as the current static directory context.
/// </summary>
public static class SinumerikProgramSearchPath
{
    private const int CurrentDocumentRank = 0;
    private const int CurrentDirectoryRank = 1;
    private const int SubprogramDirectoryRank = 2;
    private const int UserCyclesDirectoryRank = 3;
    private const int ManufacturerCyclesDirectoryRank = 4;
    private const int StandardCyclesDirectoryRank = 5;
    private const int OtherDirectoryRank = 6;
    private const int NonProcedureRank = 7;

    /// <summary>
    /// Orders matching global symbols so consumers see the procedure that SINUMERIK would
    /// search first before lower-priority or ambiguous candidates.
    /// </summary>
    public static IEnumerable<AbstractSymbol> OrderCandidates(
        IEnumerable<AbstractSymbol> candidates,
        Uri documentOfReference,
        IEnumerable<string> configuredManufacturerCyclesDirectories)
    {
        var referenceDirectory = Path.GetDirectoryName(documentOfReference.LocalPath) ?? string.Empty;
        var manufacturerDirectories = configuredManufacturerCyclesDirectories
            .Select(NormalizeDirectoryName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return candidates
            .OrderBy(candidate => GetRank(candidate, documentOfReference, referenceDirectory, manufacturerDirectories))
            .ThenBy(candidate => candidate.SourceDocument.LocalPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.IdentifierRange.Start.Line)
            .ThenBy(candidate => candidate.IdentifierRange.Start.Character);
    }

    private static int GetRank(
        AbstractSymbol candidate,
        Uri documentOfReference,
        string referenceDirectory,
        ISet<string> manufacturerDirectories)
    {
        if (candidate is not ProcedureSymbol)
        {
            return NonProcedureRank;
        }

        if (candidate.SourceDocument == documentOfReference)
        {
            return CurrentDocumentRank;
        }

        var candidateDirectory = Path.GetDirectoryName(candidate.SourceDocument.LocalPath) ?? string.Empty;
        if (candidateDirectory.Equals(referenceDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return CurrentDirectoryRank;
        }

        var directoryName = NormalizeDirectoryName(Path.GetFileName(candidateDirectory));
        return directoryName switch
        {
            "spf" => SubprogramDirectoryRank,
            "cus" => UserCyclesDirectoryRank,
            "cma" => ManufacturerCyclesDirectoryRank,
            "cst" => StandardCyclesDirectoryRank,
            _ when manufacturerDirectories.Contains(directoryName) => ManufacturerCyclesDirectoryRank,
            _ => OtherDirectoryRank
        };
    }

    private static string NormalizeDirectoryName(string directoryName)
    {
        var normalized = directoryName
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace('.', '_')
            .ToLowerInvariant();

        if (normalized.StartsWith("_n_", StringComparison.Ordinal))
        {
            normalized = normalized[3..];
        }

        if (normalized.EndsWith("_dir", StringComparison.Ordinal))
        {
            normalized = normalized[..^4];
        }

        return normalized;
    }
}
