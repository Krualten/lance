using LanceServer.Core.Symbol;
using LanceServer.Core.Workspace;
using LspTypes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Range = LspTypes.Range;

namespace LanceServerTest.Core.Workspace;

[TestClass]
public class SymbolReferenceResolverTest
{
    [TestMethod]
    public void ProcedureUseOnlyReturnsProcedures()
    {
        var source = CreateUri("MAIN.MPF");
        var procedure = CreateProcedure();
        var macro = CreateMacro();
        var use = new ProcedureUse("SHARED", CreateRange(), source, Array.Empty<ProcedureUseArgument>());

        var candidates = SymbolReferenceResolver
            .FilterCandidates(use, new AbstractSymbol[] { macro, procedure })
            .ToList();

        CollectionAssert.AreEqual(new AbstractSymbol[] { procedure }, candidates);
    }

    [TestMethod]
    public void OrdinarySymbolUseDoesNotReturnProcedures()
    {
        var source = CreateUri("MAIN.MPF");
        var procedure = CreateProcedure();
        var macro = CreateMacro();
        var use = new SymbolUse("SHARED", CreateRange(), source);

        var candidates = SymbolReferenceResolver
            .FilterCandidates(use, new AbstractSymbol[] { procedure, macro })
            .ToList();

        CollectionAssert.AreEqual(new AbstractSymbol[] { macro }, candidates);
    }

    [TestMethod]
    public void BareSymbolUseFallsBackToProcedureWhenNoDataSymbolExists()
    {
        var source = CreateUri("MAIN.MPF");
        var procedure = CreateProcedure();
        var use = new SymbolUse("SHARED", CreateRange(), source);

        var candidates = SymbolReferenceResolver
            .FilterCandidates(use, new AbstractSymbol[] { procedure })
            .ToList();

        CollectionAssert.AreEqual(new AbstractSymbol[] { procedure }, candidates);
    }

    private static ProcedureSymbol CreateProcedure()
    {
        var range = CreateRange();
        return new ProcedureSymbol(
            "SHARED",
            CreateUri("SPF.DIR", "SHARED.SPF"),
            range,
            range,
            Array.Empty<ParameterSymbol>());
    }

    private static MacroSymbol CreateMacro()
    {
        var range = CreateRange();
        return new MacroSymbol(
            "SHARED",
            CreateUri("DEF.DIR", "GLOBAL.DEF"),
            range,
            range,
            "1",
            true);
    }

    private static Uri CreateUri(params string[] pathParts)
    {
        return new Uri(Path.GetFullPath(Path.Combine(Path.GetTempPath(), Path.Combine(pathParts))));
    }

    private static Range CreateRange()
    {
        return new Range
        {
            Start = new Position(0, 0),
            End = new Position(0, 0)
        };
    }
}
