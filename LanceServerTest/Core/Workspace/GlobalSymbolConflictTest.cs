using LanceServer.Core.Symbol;
using LanceServer.Core.Workspace;
using LspTypes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Range = LspTypes.Range;

namespace LanceServerTest.Core.Workspace;

[TestClass]
public class GlobalSymbolConflictTest
{
    [TestMethod]
    public void ProceduresInDifferentWorkpiecesDoNotConflict()
    {
        var first = CreateProcedure("WKS.DIR", "FIRST.WPD", "HELPER.SPF");
        var second = CreateProcedure("WKS.DIR", "SECOND.WPD", "HELPER.SPF");

        var conflicts = GlobalSymbolConflict.GetConflicts(first, new[] { first, second });

        Assert.AreEqual(0, conflicts.Count);
    }

    [TestMethod]
    public void CycleOverridesInDifferentCycleDirectoriesDoNotConflict()
    {
        var userCycle = CreateProcedure("CUS.DIR", "HELPER.SPF");
        var standardCycle = CreateProcedure("CST.DIR", "HELPER.SPF");

        var conflicts = GlobalSymbolConflict.GetConflicts(
            userCycle,
            new[] { userCycle, standardCycle });

        Assert.AreEqual(0, conflicts.Count);
    }

    [TestMethod]
    public void ProceduresInSameDirectoryConflict()
    {
        var first = CreateProcedure("SPF.DIR", "FIRST.SPF");
        var second = CreateProcedure("SPF.DIR", "SECOND.SPF");

        var conflicts = GlobalSymbolConflict.GetConflicts(first, new[] { first, second });

        Assert.AreEqual(1, conflicts.Count);
        Assert.AreEqual(second, conflicts[0]);
    }

    [TestMethod]
    public void GlobalMacrosInDifferentDirectoriesStillConflict()
    {
        var range = CreateRange();
        var first = new MacroSymbol(
            "GLOBAL_NAME",
            CreateUri("DEF.DIR", "FIRST.DEF"),
            range,
            range,
            "1",
            true);
        var second = new MacroSymbol(
            "GLOBAL_NAME",
            CreateUri("OEM_DEF.DIR", "SECOND.DEF"),
            range,
            range,
            "2",
            true);

        var conflicts = GlobalSymbolConflict.GetConflicts(first, new[] { first, second });

        Assert.AreEqual(1, conflicts.Count);
        Assert.AreEqual(second, conflicts[0]);
    }

    private static ProcedureSymbol CreateProcedure(params string[] pathParts)
    {
        var range = CreateRange();

        return new ProcedureSymbol(
            "HELPER",
            CreateUri(pathParts),
            range,
            range,
            Array.Empty<ParameterSymbol>());
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
