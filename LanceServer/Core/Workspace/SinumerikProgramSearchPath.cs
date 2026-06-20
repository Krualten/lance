using LanceServer.Core.Symbol;

namespace LanceServer.Core.Workspace;

/// <summary>
/// Orders procedure candidates according to the standard SINUMERIK subprogram search path.
/// The document containing the reference is used as the current static directory context.
/// </summary>
public static class SinumerikProgramSearchPath
{
    internal static readonly ISet<string> StandardCycleDirectories =
        new HashSet<string>(new[] { "cus", "cma", "cst" }, StringComparer.OrdinalIgnoreCase);

    private const int ExplicitDirectoryRank = -1;
    private const int CurrentDocumentRank = 0;
    private const int CurrentDirectoryRank = 1;
    private const int SubprogramDirectoryRank = 2;
    private const int CallPathDirectoryRank = 3;
    private const int UserCyclesDirectoryRank = 4;
    private const int ManufacturerCyclesDirectoryRank = 5;
    private const int StandardCyclesDirectoryRank = 6;
    private const int OtherDirectoryRank = 7;
    private const int NonProcedureRank = 8;

    /// <summary>
    /// Orders matching global symbols so consumers see the procedure that SINUMERIK would
    /// search first before lower-priority or ambiguous candidates.
    /// </summary>
    public static IEnumerable<AbstractSymbol> OrderCandidates(
        IEnumerable<AbstractSymbol> candidates,
        Uri documentOfReference,
        IEnumerable<string> configuredManufacturerCyclesDirectories,
        string? callPath = null,
        string? explicitDirectoryPath = null,
        string? explicitFileExtension = null)
    {
        var referenceDirectory = Path.GetDirectoryName(documentOfReference.LocalPath) ?? string.Empty;
        var manufacturerDirectories = configuredManufacturerCyclesDirectories
            .Select(NormalizeDirectoryName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalizedCallPath = NormalizeProgramDirectoryPath(callPath);
        var normalizedExplicitDirectoryPath = NormalizeProgramDirectoryPath(explicitDirectoryPath);
        var filteredCandidates = candidates;

        if (normalizedExplicitDirectoryPath.Count > 0)
        {
            filteredCandidates = filteredCandidates.Where(candidate =>
                DirectoryMatchesProgramPath(
                    Path.GetDirectoryName(candidate.SourceDocument.LocalPath) ?? string.Empty,
                    normalizedExplicitDirectoryPath,
                    manufacturerDirectories));
        }

        if (!string.IsNullOrEmpty(explicitFileExtension))
        {
            filteredCandidates = filteredCandidates.Where(candidate =>
                Path.GetExtension(candidate.SourceDocument.LocalPath).Equals(
                    explicitFileExtension,
                    StringComparison.OrdinalIgnoreCase));
        }

        return filteredCandidates
            .OrderBy(candidate => GetRank(
                candidate,
                documentOfReference,
                referenceDirectory,
                manufacturerDirectories,
                normalizedCallPath,
                normalizedExplicitDirectoryPath))
            .ThenBy(candidate => candidate.SourceDocument.LocalPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.IdentifierRange.Start.Line)
            .ThenBy(candidate => candidate.IdentifierRange.Start.Character);
    }

    private static int GetRank(
        AbstractSymbol candidate,
        Uri documentOfReference,
        string referenceDirectory,
        ISet<string> manufacturerDirectories,
        IReadOnlyList<string> normalizedCallPath,
        IReadOnlyList<string> normalizedExplicitDirectoryPath)
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
        if (DirectoryMatchesProgramPath(
                candidateDirectory,
                normalizedExplicitDirectoryPath,
                manufacturerDirectories))
        {
            return ExplicitDirectoryRank;
        }

        if (candidateDirectory.Equals(referenceDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return CurrentDirectoryRank;
        }

        if (DirectoryMatchesProgramPath(candidateDirectory, normalizedCallPath, manufacturerDirectories))
        {
            return CallPathDirectoryRank;
        }

        foreach (var directoryName in NormalizeProgramDirectoryPath(candidateDirectory).Reverse())
        {
            var rank = directoryName switch
            {
                "spf" => SubprogramDirectoryRank,
                "cus" => UserCyclesDirectoryRank,
                "cma" => ManufacturerCyclesDirectoryRank,
                "cst" => StandardCyclesDirectoryRank,
                _ when manufacturerDirectories.Contains(directoryName) => ManufacturerCyclesDirectoryRank,
                _ => OtherDirectoryRank
            };

            if (rank != OtherDirectoryRank)
            {
                return rank;
            }
        }

        return OtherDirectoryRank;
    }

    internal static bool IsWithinCycleDirectory(
        string directoryPath,
        IEnumerable<string> configuredManufacturerCyclesDirectories)
    {
        var configuredDirectories = configuredManufacturerCyclesDirectories
            .Select(NormalizeDirectoryName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return NormalizeProgramDirectoryPath(directoryPath).Any(directoryName =>
            StandardCycleDirectories.Contains(directoryName)
            || configuredDirectories.Contains(directoryName));
    }

    internal static string NormalizeDirectoryName(string? directoryName)
    {
        directoryName ??= string.Empty;
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

    internal static IReadOnlyList<string> NormalizeProgramDirectoryPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Array.Empty<string>();
        }

        return path
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeDirectoryName)
            .Where(segment => !string.IsNullOrEmpty(segment))
            .ToArray();
    }

    private static bool DirectoryMatchesPath(
        string candidateDirectory,
        IReadOnlyList<string> normalizedCallPath)
    {
        if (normalizedCallPath.Count == 0)
        {
            return false;
        }

        var candidateSegments = NormalizeProgramDirectoryPath(candidateDirectory);
        if (candidateSegments.Count < normalizedCallPath.Count)
        {
            return false;
        }

        var offset = candidateSegments.Count - normalizedCallPath.Count;
        for (var index = 0; index < normalizedCallPath.Count; index++)
        {
            if (!candidateSegments[offset + index].Equals(
                    normalizedCallPath[index],
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool DirectoryMatchesProgramPath(
        string candidateDirectory,
        IReadOnlyList<string> normalizedProgramPath,
        ISet<string> manufacturerDirectories)
    {
        if (DirectoryMatchesPath(candidateDirectory, normalizedProgramPath))
        {
            return true;
        }

        if (normalizedProgramPath.Count != 1)
        {
            return false;
        }

        var requestedRoot = normalizedProgramPath[0];
        if (!StandardCycleDirectories.Contains(requestedRoot)
            && !manufacturerDirectories.Contains(requestedRoot))
        {
            return false;
        }

        return NormalizeProgramDirectoryPath(candidateDirectory)
            .Contains(requestedRoot, StringComparer.OrdinalIgnoreCase);
    }
}
