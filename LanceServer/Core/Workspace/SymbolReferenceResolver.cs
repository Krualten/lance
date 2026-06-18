using LanceServer.Core.Symbol;

namespace LanceServer.Core.Workspace;

/// <summary>
/// Filters same-named symbols according to the syntactic kind of a symbol use.
/// </summary>
public static class SymbolReferenceResolver
{
    /// <summary>
    /// Procedure calls and declarations can only reference procedures. Other symbol uses
    /// can reference variables, macros, labels or block numbers, but not procedures.
    /// </summary>
    public static IEnumerable<AbstractSymbol> FilterCandidates(
        AbstractSymbolUse symbolUse,
        IEnumerable<AbstractSymbol> candidates)
    {
        var referencesProcedure = symbolUse is ProcedureUse or DeclarationProcedureUse;
        return candidates.Where(candidate => candidate is ProcedureSymbol == referencesProcedure);
    }
}
