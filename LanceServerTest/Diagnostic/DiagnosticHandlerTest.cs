using LanceServer.Core.Configuration;
using LanceServer.Core.Configuration.DataModel;
using LanceServer.Core.Symbol;
using LanceServer.Core.Workspace;
using LanceServer.Parser;
using LanceServer.Preprocessor;
using LanceServer.RequestHandler.Diagnostic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LanceServerTest.Diagnostic;

[TestClass]
public class DiagnosticHandlerTest
{
    [TestMethod]
    public void LiteralCallPathResolvesProcedureInAnotherWorkpiece()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-callpath-" + Guid.NewGuid());
        var workpiecesDirectory = Path.Combine(directory, "WKS.DIR");
        var mainDirectory = Path.Combine(workpiecesDirectory, "MAIN.WPD");
        var libraryDirectory = Path.Combine(workpiecesDirectory, "LIBRARY.WPD");
        Directory.CreateDirectory(mainDirectory);
        Directory.CreateDirectory(libraryDirectory);
        var helperPath = Path.Combine(libraryDirectory, "TEST_HELPER.SPF");
        var mainPath = Path.Combine(mainDirectory, "TEST_MAIN.MPF");
        File.WriteAllText(
            helperPath,
            @"PROC TEST_HELPER()
RET
ENDPROC
");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
CALLPATH(""/_N_WKS_DIR/_N_LIBRARY_WPD"")
TEST_HELPER()
RET
ENDPROC
");

        try
        {
            var configurationManager = CreateConfigurationManager();
            var workspace = new Workspace(
                new ParserManager(),
                new PlaceholderPreprocessor(configurationManager),
                configurationManager);
            workspace.GetSymbolisedDocument(new Uri(helperPath));
            var mainDocument = workspace.GetSymbolUseExtractedDocument(new Uri(mainPath));

            var diagnostics = new DiagnosticHandler().HandleRequest(mainDocument, workspace).Items;

            Assert.AreEqual(0, mainDocument.ParserDiagnostics.Count);
            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.Equals("Cannot resolve symbol TEST_HELPER.", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void LiteralIndirectCallResolvesExplicitProcedurePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-indirect-call-" + Guid.NewGuid());
        var workpiecesDirectory = Path.Combine(directory, "WKS.DIR");
        var mainDirectory = Path.Combine(workpiecesDirectory, "MAIN.WPD");
        var libraryDirectory = Path.Combine(workpiecesDirectory, "LIBRARY.WPD");
        Directory.CreateDirectory(mainDirectory);
        Directory.CreateDirectory(libraryDirectory);
        var localHelperPath = Path.Combine(mainDirectory, "TEST_HELPER.SPF");
        var libraryHelperPath = Path.Combine(libraryDirectory, "TEST_HELPER.SPF");
        var mainPath = Path.Combine(mainDirectory, "TEST_MAIN.MPF");
        File.WriteAllText(localHelperPath, "PROC TEST_HELPER()" + Environment.NewLine + "RET" + Environment.NewLine + "ENDPROC");
        File.WriteAllText(libraryHelperPath, "PROC TEST_HELPER()" + Environment.NewLine + "RET" + Environment.NewLine + "ENDPROC");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
CALL ""/_N_WKS_DIR/_N_LIBRARY_WPD/_N_TEST_HELPER_SPF""
RET
ENDPROC
");

        try
        {
            var configurationManager = CreateConfigurationManager();
            var workspace = new Workspace(
                new ParserManager(),
                new PlaceholderPreprocessor(configurationManager),
                configurationManager);
            workspace.GetSymbolisedDocument(new Uri(localHelperPath));
            var libraryUri = new Uri(libraryHelperPath);
            workspace.GetSymbolisedDocument(libraryUri);
            var mainDocument = workspace.GetSymbolUseExtractedDocument(new Uri(mainPath));
            var procedureUse = mainDocument.SymbolUseTable.GetAll().OfType<ProcedureUse>().Single();

            var resolvedProcedure = workspace.GetSymbols(procedureUse).OfType<ProcedureSymbol>().First();
            var diagnostics = new DiagnosticHandler().HandleRequest(mainDocument, workspace).Items;

            Assert.AreEqual(libraryUri, resolvedProcedure.SourceDocument);
            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.Equals("Cannot resolve symbol TEST_HELPER.", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void ModalManufacturerCycleCallWithParametersResolvesWithoutExtern()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-mcall-" + Guid.NewGuid());
        var cyclesDirectory = Path.Combine(directory, "CMA.DIR");
        Directory.CreateDirectory(cyclesDirectory);
        var helperPath = Path.Combine(cyclesDirectory, "TEST_HELPER.SPF");
        var mainPath = Path.Combine(directory, "TEST_MAIN.MPF");
        File.WriteAllText(
            helperPath,
            @"PROC TEST_HELPER(INT axisNo, REAL targetPos)
RET
ENDPROC
");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
DEF INT mainAxis
DEF REAL mainPos
MCALL TEST_HELPER(mainAxis, mainPos)
MCALL
RET
ENDPROC
");

        try
        {
            var configurationManager = CreateConfigurationManager();
            var workspace = new Workspace(
                new ParserManager(),
                new PlaceholderPreprocessor(configurationManager),
                configurationManager);
            workspace.GetSymbolisedDocument(new Uri(helperPath));
            var mainDocument = workspace.GetSymbolUseExtractedDocument(new Uri(mainPath));

            var diagnostics = new DiagnosticHandler().HandleRequest(mainDocument, workspace).Items;

            Assert.AreEqual(
                0,
                mainDocument.ParserDiagnostics.Count,
                string.Join(Environment.NewLine, mainDocument.ParserDiagnostics.Select(diagnostic => diagnostic.Message)));
            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.StartsWith("Missing extern declaration", StringComparison.Ordinal)));
            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.Equals("Cannot resolve symbol TEST_HELPER.", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void MissingExplicitIsoCallPathDoesNotResolveLocalHomonym()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-isocall-" + Guid.NewGuid());
        var workpieceDirectory = Path.Combine(directory, "WKS.DIR", "MAIN.WPD");
        Directory.CreateDirectory(workpieceDirectory);
        var localHelperPath = Path.Combine(workpieceDirectory, "ISO_HELPER.SPF");
        var mainPath = Path.Combine(workpieceDirectory, "TEST_MAIN.MPF");
        File.WriteAllText(
            localHelperPath,
            "PROC ISO_HELPER()" + Environment.NewLine + "RET" + Environment.NewLine + "ENDPROC");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
ISOCALL ""/_N_WKS_DIR/_N_MISSING_WPD/_N_ISO_HELPER_SPF""
RET
ENDPROC
");

        try
        {
            var configurationManager = CreateConfigurationManager();
            var workspace = new Workspace(
                new ParserManager(),
                new PlaceholderPreprocessor(configurationManager),
                configurationManager);
            workspace.GetSymbolisedDocument(new Uri(localHelperPath));
            var mainDocument = workspace.GetSymbolUseExtractedDocument(new Uri(mainPath));

            var diagnostics = new DiagnosticHandler().HandleRequest(mainDocument, workspace).Items;

            Assert.IsTrue(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.Equals("Cannot resolve symbol ISO_HELPER.", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void LiteralIsoCallResolvesExactSpfPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-isocall-exact-" + Guid.NewGuid());
        var mainDirectory = Path.Combine(directory, "WKS.DIR", "MAIN.WPD");
        var libraryDirectory = Path.Combine(directory, "WKS.DIR", "LIBRARY.WPD");
        Directory.CreateDirectory(mainDirectory);
        Directory.CreateDirectory(libraryDirectory);
        var localHelperPath = Path.Combine(mainDirectory, "ISO_HELPER.SPF");
        var libraryMpfPath = Path.Combine(libraryDirectory, "ISO_HELPER.MPF");
        var librarySpfPath = Path.Combine(libraryDirectory, "ISO_HELPER.SPF");
        var mainPath = Path.Combine(mainDirectory, "TEST_MAIN.MPF");
        var helperCode = "PROC ISO_HELPER()" + Environment.NewLine + "RET" + Environment.NewLine + "ENDPROC";
        File.WriteAllText(localHelperPath, helperCode);
        File.WriteAllText(libraryMpfPath, helperCode);
        File.WriteAllText(librarySpfPath, helperCode);
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
ISOCALL ""/_N_WKS_DIR/_N_LIBRARY_WPD/_N_ISO_HELPER_SPF""
RET
ENDPROC
");

        try
        {
            var configurationManager = CreateConfigurationManager();
            var workspace = new Workspace(
                new ParserManager(),
                new PlaceholderPreprocessor(configurationManager),
                configurationManager);
            workspace.GetSymbolisedDocument(new Uri(localHelperPath));
            workspace.GetSymbolisedDocument(new Uri(libraryMpfPath));
            var librarySpfUri = new Uri(librarySpfPath);
            workspace.GetSymbolisedDocument(librarySpfUri);
            var mainDocument = workspace.GetSymbolUseExtractedDocument(new Uri(mainPath));
            var procedureUse = mainDocument.SymbolUseTable.GetAll().OfType<ProcedureUse>().Single();

            var resolvedProcedures = workspace.GetSymbols(procedureUse).OfType<ProcedureSymbol>().ToList();
            var diagnostics = new DiagnosticHandler().HandleRequest(mainDocument, workspace).Items;

            Assert.AreEqual(1, resolvedProcedures.Count);
            Assert.AreEqual(librarySpfUri, resolvedProcedures[0].SourceDocument);
            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.Equals("Cannot resolve symbol ISO_HELPER.", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [DataTestMethod]
    [DataRow("cus.dir")]
    [DataRow("cma.dir")]
    [DataRow("cst.dir")]
    [DataRow("_N_CUS_DIR")]
    [DataRow("_N_CMA_DIR")]
    [DataRow("_N_CST_DIR")]
    public void CycleCallWithParametersDoesNotRequireExternDeclaration(string cycleDirectoryName)
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "lance-extern-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var cycleDirectory = Path.Combine(directory, cycleDirectoryName);
        Directory.CreateDirectory(cycleDirectory);
        var helperPath = Path.Combine(cycleDirectory, "TEST_HELPER.SPF");
        var mainPath = Path.Combine(directory, "TEST_MAIN.MPF");
        File.WriteAllText(
            helperPath,
            @"PROC TEST_HELPER(INT axisNo, REAL targetPos)
RET
ENDPROC
");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
DEF INT mainAxis
DEF REAL mainPos
TEST_HELPER(mainAxis, mainPos)
RET
ENDPROC
");

        try
        {
            var configurationManager = CreateConfigurationManager();
            var workspace = new Workspace(
                new ParserManager(),
                new PlaceholderPreprocessor(configurationManager),
                configurationManager);
            var helperUri = new Uri(helperPath);
            var mainUri = new Uri(mainPath);
            workspace.GetSymbolisedDocument(helperUri);
            var mainDocument = workspace.GetSymbolUseExtractedDocument(mainUri);

            // Act
            var diagnostics = new DiagnosticHandler().HandleRequest(mainDocument, workspace).Items;

            // Assert
            Assert.AreEqual(
                0,
                mainDocument.ParserDiagnostics.Count,
                string.Join(Environment.NewLine, mainDocument.ParserDiagnostics.Select(diagnostic => diagnostic.Message)));
            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.StartsWith("Missing extern declaration", StringComparison.Ordinal)));
            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.Equals("Cannot resolve symbol TEST_HELPER.", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static ConfigurationManager CreateConfigurationManager()
    {
        var configuration = new ServerConfiguration
        {
            SymbolTableConfiguration = new SymbolTableConfiguration
            {
                DefinitionFileExtensions = new[] { ".def" },
                MainProcedureFileExtensions = new[] { ".mpf" },
                SubProcedureFileExtensions = new[] { ".spf" },
                ManufacturerCyclesDirectories = new[] { "cma.dir" }
            },
            PlaceholderPreprocessor = new CustomPreprocessorConfiguration
            {
                FileExtensions = Array.Empty<string>(),
                Placeholders = Array.Empty<string>(),
                PlaceholderType = PlaceholderType.String
            }
        };

        return new ConfigurationManager(new DocumentationConfiguration(), Array.Empty<Uri>(), configuration);
    }
}
