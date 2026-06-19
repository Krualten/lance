using LanceServer.Core.Symbol;
using LanceServer.Core.Workspace;
using LspTypes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Range = LspTypes.Range;

namespace LanceServerTest.Core.Workspace;

[TestClass]
public class SinumerikProgramSearchPathTest
{
    [TestMethod]
    public void ProcedureCandidatesFollowSinumerikDirectorySearchOrder()
    {
        // Arrange
        var reference = CreateUri("NC", "WKS.DIR", "PART.WPD", "MAIN.MPF");
        var candidates = new[]
        {
            CreateProcedure("NC", "OTHER.DIR", "HELPER.SPF"),
            CreateProcedure("NC", "_N_CST_DIR", "HELPER.SPF"),
            CreateProcedure("NC", "CMA.DIR", "HELPER.SPF"),
            CreateProcedure("NC", "_N_CUS_DIR", "HELPER.SPF"),
            CreateProcedure("NC", "SPF.DIR", "HELPER.SPF"),
            CreateProcedure("NC", "WKS.DIR", "PART.WPD", "HELPER.SPF")
        };

        // Act
        var orderedPaths = SinumerikProgramSearchPath
            .OrderCandidates(candidates, reference, Array.Empty<string>())
            .Select(symbol => symbol.SourceDocument.LocalPath)
            .ToList();

        // Assert
        CollectionAssert.AreEqual(
            new[]
            {
                CreatePath("NC", "WKS.DIR", "PART.WPD", "HELPER.SPF"),
                CreatePath("NC", "SPF.DIR", "HELPER.SPF"),
                CreatePath("NC", "_N_CUS_DIR", "HELPER.SPF"),
                CreatePath("NC", "CMA.DIR", "HELPER.SPF"),
                CreatePath("NC", "_N_CST_DIR", "HELPER.SPF"),
                CreatePath("NC", "OTHER.DIR", "HELPER.SPF")
            },
            orderedPaths);
    }

    [TestMethod]
    public void ConfiguredManufacturerDirectoryUsesManufacturerCyclePriority()
    {
        // Arrange
        var reference = CreateUri("NC", "WKS.DIR", "PART.WPD", "MAIN.MPF");
        var candidates = new[]
        {
            CreateProcedure("NC", "OTHER.DIR", "HELPER.SPF"),
            CreateProcedure("NC", "OEM_CYCLES", "HELPER.SPF")
        };

        // Act
        var orderedPaths = SinumerikProgramSearchPath
            .OrderCandidates(candidates, reference, new[] { "oem_cycles" })
            .Select(symbol => symbol.SourceDocument.LocalPath)
            .ToList();

        // Assert
        Assert.AreEqual(CreatePath("NC", "OEM_CYCLES", "HELPER.SPF"), orderedPaths[0]);
    }

    [TestMethod]
    public void NestedManufacturerCycleUsesManufacturerCyclePriority()
    {
        var reference = CreateUri("NC", "WKS.DIR", "PART.WPD", "MAIN.MPF");
        var candidates = new[]
        {
            CreateProcedure("NC", "OTHER.DIR", "HELPER.SPF"),
            CreateProcedure("NC", "CMA.DIR", "ATC", "HELPER.SPF")
        };

        var orderedPaths = SinumerikProgramSearchPath
            .OrderCandidates(candidates, reference, Array.Empty<string>())
            .Select(symbol => symbol.SourceDocument.LocalPath)
            .ToList();

        Assert.AreEqual(CreatePath("NC", "CMA.DIR", "ATC", "HELPER.SPF"), orderedPaths[0]);
    }

    [TestMethod]
    public void CallPathDirectoryIsSearchedBeforeUserCycles()
    {
        var reference = CreateUri("NC", "WKS.DIR", "PART.WPD", "MAIN.MPF");
        var candidates = new[]
        {
            CreateProcedure("NC", "CUS.DIR", "HELPER.SPF"),
            CreateProcedure("NC", "WKS.DIR", "LIBRARY.WPD", "HELPER.SPF")
        };

        var orderedPaths = SinumerikProgramSearchPath
            .OrderCandidates(
                candidates,
                reference,
                Array.Empty<string>(),
                "/_N_WKS_DIR/_N_LIBRARY_WPD")
            .Select(symbol => symbol.SourceDocument.LocalPath)
            .ToList();

        Assert.AreEqual(
            CreatePath("NC", "WKS.DIR", "LIBRARY.WPD", "HELPER.SPF"),
            orderedPaths[0]);
    }

    [TestMethod]
    public void ExplicitProgramDirectoryOverridesCurrentDirectory()
    {
        var reference = CreateUri("NC", "WKS.DIR", "PART.WPD", "MAIN.MPF");
        var candidates = new[]
        {
            CreateProcedure("NC", "WKS.DIR", "PART.WPD", "HELPER.SPF"),
            CreateProcedure("NC", "WKS.DIR", "LIBRARY.WPD", "HELPER.SPF")
        };

        var orderedPaths = SinumerikProgramSearchPath
            .OrderCandidates(
                candidates,
                reference,
                Array.Empty<string>(),
                explicitDirectoryPath: "/_N_WKS_DIR/_N_LIBRARY_WPD")
            .Select(symbol => symbol.SourceDocument.LocalPath)
            .ToList();

        Assert.AreEqual(
            CreatePath("NC", "WKS.DIR", "LIBRARY.WPD", "HELPER.SPF"),
            orderedPaths[0]);
    }

    [TestMethod]
    public void MissingExplicitProgramDirectoryDoesNotFallBackToHomonym()
    {
        var reference = CreateUri("NC", "WKS.DIR", "PART.WPD", "MAIN.MPF");
        var candidates = new[]
        {
            CreateProcedure("NC", "WKS.DIR", "PART.WPD", "HELPER.SPF"),
            CreateProcedure("NC", "CUS.DIR", "HELPER.SPF")
        };

        var orderedPaths = SinumerikProgramSearchPath
            .OrderCandidates(
                candidates,
                reference,
                Array.Empty<string>(),
                explicitDirectoryPath: "/_N_WKS_DIR/_N_MISSING_WPD",
                explicitFileExtension: ".spf")
            .ToList();

        Assert.AreEqual(0, orderedPaths.Count);
    }

    [TestMethod]
    public void ExplicitProgramTypeFiltersMpfAndSpfHomonyms()
    {
        var reference = CreateUri("NC", "WKS.DIR", "PART.WPD", "MAIN.MPF");
        var candidates = new[]
        {
            CreateProcedure("NC", "WKS.DIR", "LIBRARY.WPD", "HELPER.MPF"),
            CreateProcedure("NC", "WKS.DIR", "LIBRARY.WPD", "HELPER.SPF")
        };

        var orderedPaths = SinumerikProgramSearchPath
            .OrderCandidates(
                candidates,
                reference,
                Array.Empty<string>(),
                explicitDirectoryPath: "/_N_WKS_DIR/_N_LIBRARY_WPD",
                explicitFileExtension: ".spf")
            .Select(symbol => symbol.SourceDocument.LocalPath)
            .ToList();

        CollectionAssert.AreEqual(
            new[] { CreatePath("NC", "WKS.DIR", "LIBRARY.WPD", "HELPER.SPF") },
            orderedPaths);
    }

    private static ProcedureSymbol CreateProcedure(params string[] pathParts)
    {
        var range = new Range
        {
            Start = new Position(0, 0),
            End = new Position(0, 0)
        };

        return new ProcedureSymbol(
            "HELPER",
            CreateUri(pathParts),
            range,
            range,
            Array.Empty<ParameterSymbol>());
    }

    private static Uri CreateUri(params string[] pathParts)
    {
        return new Uri(CreatePath(pathParts));
    }

    private static string CreatePath(params string[] pathParts)
    {
        return Path.GetFullPath(Path.Combine(Path.GetTempPath(), Path.Combine(pathParts)));
    }
}
