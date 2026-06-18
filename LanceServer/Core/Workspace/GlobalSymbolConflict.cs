using LanceServer.Core.Symbol;

namespace LanceServer.Core.Workspace;

/// <summary>
/// Determines whether same-named global symbols occupy the same SINUMERIK namespace.
/// </summary>
public static class GlobalSymbolConflict
{
    /// <summary>
    /// Returns same-named symbols that conflict with the referenced symbol.
    /// Procedures in different directories are valid search-path alternatives; other
    /// global symbols retain workspace-wide uniqueness.
    /// </summary>
    public static IList<AbstractSymbol> GetConflicts(
        AbstractSymbol symbol,
        IEnumerable<AbstractSymbol> sameNamedSymbols)
    {
        return sameNamedSymbols
            .Where(candidate => !candidate.Equals(symbol))
            .Where(candidate => ConflictsWith(symbol, candidate))
            .ToList();
    }

    private static bool ConflictsWith(AbstractSymbol symbol, AbstractSymbol candidate)
    {
        if (symbol is not ProcedureSymbol || candidate is not ProcedureSymbol)
        {
            return true;
        }

        var symbolDirectory = Path.GetDirectoryName(symbol.SourceDocument.LocalPath) ?? string.Empty;
        var candidateDirectory = Path.GetDirectoryName(candidate.SourceDocument.LocalPath) ?? string.Empty;
        return symbolDirectory.Equals(candidateDirectory, StringComparison.OrdinalIgnoreCase);
    }
}
