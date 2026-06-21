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
    public void LiteralCallToCycleRootResolvesNestedDevelopmentCycle()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-nested-cycle-call-" + Guid.NewGuid());
        var bootDirectory = Path.Combine(directory, "CMA.DIR", "BOOT");
        var genericDirectory = Path.Combine(directory, "CMA.DIR", "GENERIC");
        Directory.CreateDirectory(bootDirectory);
        Directory.CreateDirectory(genericDirectory);
        var helperPath = Path.Combine(genericDirectory, "TRA_FC21.SPF");
        var mainPath = Path.Combine(bootDirectory, "PROG_EVENT.SPF");
        File.WriteAllText(helperPath, "M17" + Environment.NewLine);
        File.WriteAllText(
            mainPath,
            "CALL \"/_N_CMA_DIR/_N_TRA_FC21_SPF\"" + Environment.NewLine +
            "M17" + Environment.NewLine);

        try
        {
            var configurationManager = CreateConfigurationManager();
            var workspace = new Workspace(
                new ParserManager(),
                new PlaceholderPreprocessor(configurationManager),
                configurationManager);
            var helperUri = new Uri(helperPath);
            workspace.GetSymbolisedDocument(helperUri);
            var mainDocument = workspace.GetSymbolUseExtractedDocument(new Uri(mainPath));
            var procedureUse = mainDocument.SymbolUseTable.GetAll().OfType<ProcedureUse>().Single();

            var resolvedProcedure = workspace.GetSymbols(procedureUse).OfType<ProcedureSymbol>().Single();
            var diagnostics = new DiagnosticHandler().HandleRequest(mainDocument, workspace).Items;

            Assert.AreEqual(helperUri, resolvedProcedure.SourceDocument);
            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.Equals("Cannot resolve symbol TRA_FC21.", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void AbsolutePcallResolvesStrictPathAndTransfersParametersWithoutExtern()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-pcall-" + Guid.NewGuid());
        var mainDirectory = Path.Combine(directory, "WKS.DIR", "MAIN.WPD");
        var libraryDirectory = Path.Combine(directory, "WKS.DIR", "LIBRARY.WPD");
        Directory.CreateDirectory(mainDirectory);
        Directory.CreateDirectory(libraryDirectory);
        var localHelperPath = Path.Combine(mainDirectory, "TEST_HELPER.SPF");
        var libraryHelperPath = Path.Combine(libraryDirectory, "TEST_HELPER.SPF");
        var mainPath = Path.Combine(mainDirectory, "TEST_MAIN.MPF");
        var helperCode =
            "PROC TEST_HELPER(INT axisNo, REAL targetPos)" + Environment.NewLine +
            "RET" + Environment.NewLine +
            "ENDPROC";
        File.WriteAllText(localHelperPath, helperCode);
        File.WriteAllText(libraryHelperPath, helperCode);
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
DEF INT mainAxis
DEF REAL mainPos
PCALL/_N_WKS_DIR/_N_LIBRARY_WPD/TEST_HELPER(mainAxis, mainPos)
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

            var resolvedProcedure = workspace.GetSymbols(procedureUse).OfType<ProcedureSymbol>().Single();
            var diagnostics = new DiagnosticHandler().HandleRequest(mainDocument, workspace).Items;

            Assert.AreEqual(
                0,
                mainDocument.ParserDiagnostics.Count,
                string.Join(Environment.NewLine, mainDocument.ParserDiagnostics.Select(diagnostic => diagnostic.Message)));
            Assert.AreEqual(libraryUri, resolvedProcedure.SourceDocument);
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
    public void MissingAbsolutePcallPathDoesNotResolveLocalHomonym()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-pcall-missing-" + Guid.NewGuid());
        var mainDirectory = Path.Combine(directory, "WKS.DIR", "MAIN.WPD");
        Directory.CreateDirectory(mainDirectory);
        var localHelperPath = Path.Combine(mainDirectory, "TEST_HELPER.SPF");
        var mainPath = Path.Combine(mainDirectory, "TEST_MAIN.MPF");
        File.WriteAllText(
            localHelperPath,
            "PROC TEST_HELPER(INT value)" + Environment.NewLine + "RET" + Environment.NewLine + "ENDPROC");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
DEF INT value
PCALL/_N_WKS_DIR/_N_MISSING_WPD/TEST_HELPER(value)
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

            Assert.AreEqual(0, mainDocument.ParserDiagnostics.Count);
            Assert.IsTrue(
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
    public void ManufacturerCycleAllowsOmittedValueParameters()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-cycle-omitted-value-" + Guid.NewGuid());
        var cyclesDirectory = Path.Combine(directory, "CMA.DIR");
        Directory.CreateDirectory(cyclesDirectory);
        var helperPath = Path.Combine(cyclesDirectory, "OEM_CYCLE.SPF");
        var mainPath = Path.Combine(directory, "TEST_MAIN.MPF");
        File.WriteAllText(
            helperPath,
            @"PROC OEM_CYCLE(REAL depth, INT mode, BOOL enabled)
RET
ENDPROC
");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
DEF REAL depth
OEM_CYCLE(depth,,)
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
                    diagnostic.Message.Equals(
                        "Procedure arguments do not match the expected parameter interface.",
                        StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void ManufacturerCycleRejectsOmittedReferenceParameter()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-cycle-omitted-reference-" + Guid.NewGuid());
        var cyclesDirectory = Path.Combine(directory, "CMA.DIR");
        Directory.CreateDirectory(cyclesDirectory);
        var helperPath = Path.Combine(cyclesDirectory, "OEM_CYCLE.SPF");
        var mainPath = Path.Combine(directory, "TEST_MAIN.MPF");
        File.WriteAllText(
            helperPath,
            @"PROC OEM_CYCLE(REAL depth, VAR INT result, AXIS machiningAxis)
RET
ENDPROC
");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
DEF REAL depth
DEF AXIS machiningAxis
OEM_CYCLE(depth,,machiningAxis)
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
            Assert.IsTrue(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.Equals(
                        "Procedure arguments do not match the expected parameter interface.",
                        StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void ManufacturerCycleReportsKnownArgumentTypeMismatch()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-cycle-type-mismatch-" + Guid.NewGuid());
        var cyclesDirectory = Path.Combine(directory, "CMA.DIR");
        Directory.CreateDirectory(cyclesDirectory);
        var helperPath = Path.Combine(cyclesDirectory, "OEM_CYCLE.SPF");
        var mainPath = Path.Combine(directory, "TEST_MAIN.MPF");
        File.WriteAllText(
            helperPath,
            @"PROC OEM_CYCLE(REAL depth, VAR INT result, STRING[20] label, AXIS machiningAxis)
RET
ENDPROC
");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
DEF REAL wrongResult
OEM_CYCLE(10, wrongResult, 5, X)
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
            Assert.IsTrue(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.StartsWith(
                        "Argument 2 does not match parameter result",
                        StringComparison.Ordinal)));
            Assert.IsTrue(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.StartsWith(
                        "Argument 3 does not match parameter label",
                        StringComparison.Ordinal)));
            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.StartsWith(
                        "Argument 1 does not match",
                        StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void ManufacturerCycleAcceptsExactReferenceAndNumericValueConversion()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-cycle-type-match-" + Guid.NewGuid());
        var cyclesDirectory = Path.Combine(directory, "CMA.DIR");
        Directory.CreateDirectory(cyclesDirectory);
        var helperPath = Path.Combine(cyclesDirectory, "OEM_CYCLE.SPF");
        var mainPath = Path.Combine(directory, "TEST_MAIN.MPF");
        File.WriteAllText(
            helperPath,
            @"PROC OEM_CYCLE(REAL depth, VAR INT result, STRING[20] label, AXIS machiningAxis)
RET
ENDPROC
");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
DEF INT result
DEF STRING[20] label
OEM_CYCLE(10, result, label, X)
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
                    diagnostic.Message.StartsWith("Argument ", StringComparison.Ordinal)));
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

    [TestMethod]
    public void LiteralExternalCallResolvesWorkspaceFileByRelativePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-extcall-" + Guid.NewGuid());
        var mainDirectory = Path.Combine(directory, "programs");
        var externalDirectory = Path.Combine(directory, "external", "programs");
        Directory.CreateDirectory(mainDirectory);
        Directory.CreateDirectory(externalDirectory);
        var localHelperPath = Path.Combine(mainDirectory, "EXT_HELPER.SPF");
        var externalHelperPath = Path.Combine(externalDirectory, "EXT_HELPER.SPF");
        var mainPath = Path.Combine(mainDirectory, "TEST_MAIN.MPF");
        var helperCode = "PROC EXT_HELPER()" + Environment.NewLine + "RET" + Environment.NewLine + "ENDPROC";
        File.WriteAllText(localHelperPath, helperCode);
        File.WriteAllText(externalHelperPath, helperCode);
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
EXTCALL(""external/programs/EXT_HELPER.SPF"")
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
            var externalHelperUri = new Uri(externalHelperPath);
            workspace.GetSymbolisedDocument(externalHelperUri);
            var mainDocument = workspace.GetSymbolUseExtractedDocument(new Uri(mainPath));
            var procedureUse = mainDocument.SymbolUseTable.GetAll().OfType<ProcedureUse>().Single();

            var resolvedProcedures = workspace.GetSymbols(procedureUse).OfType<ProcedureSymbol>().ToList();
            var diagnostics = new DiagnosticHandler().HandleRequest(mainDocument, workspace).Items;

            Assert.AreEqual(1, resolvedProcedures.Count);
            Assert.AreEqual(externalHelperUri, resolvedProcedures[0].SourceDocument);
            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.Equals("Cannot resolve symbol EXT_HELPER.", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void MissingLiteralExternalPathDoesNotResolveLocalHomonym()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-extcall-missing-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var localHelperPath = Path.Combine(directory, "EXT_HELPER.SPF");
        var mainPath = Path.Combine(directory, "TEST_MAIN.MPF");
        File.WriteAllText(
            localHelperPath,
            "PROC EXT_HELPER()" + Environment.NewLine + "RET" + Environment.NewLine + "ENDPROC");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
EXTCALL(""missing/EXT_HELPER.SPF"")
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
                    diagnostic.Message.Equals("Cannot resolve symbol EXT_HELPER.", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void CallBlockResolvesProgramAndCallerStringVariables()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-call-block-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var contourPath = Path.Combine(directory, "CONTOUR.SPF");
        var mainPath = Path.Combine(directory, "TEST_MAIN.MPF");
        File.WriteAllText(
            contourPath,
            @"PROC CONTOUR()
LABEL_1:
RET
LABEL_2:
ENDPROC
");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
DEF STRING[20] startLabel
DEF STRING[20] endLabel
startLabel=""LABEL_1""
endLabel=""LABEL_2""
CALL ""CONTOUR"" BLOCK startLabel TO endLabel
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
            workspace.GetSymbolisedDocument(new Uri(contourPath));
            var mainDocument = workspace.GetSymbolUseExtractedDocument(new Uri(mainPath));

            var diagnostics = new DiagnosticHandler().HandleRequest(mainDocument, workspace).Items;

            Assert.AreEqual(
                0,
                mainDocument.ParserDiagnostics.Count,
                string.Join(Environment.NewLine, mainDocument.ParserDiagnostics.Select(diagnostic => diagnostic.Message)));
            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.StartsWith("Cannot resolve symbol", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void CallBlockResolvesLiteralLabelsInTargetProgram()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-call-block-labels-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var contourPath = Path.Combine(directory, "CONTOUR.SPF");
        var mainPath = Path.Combine(directory, "TEST_MAIN.MPF");
        File.WriteAllText(
            contourPath,
            @"PROC CONTOUR()
START_SECTION:
RET
END_SECTION:
ENDPROC
");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
CALL ""CONTOUR"" BLOCK START_SECTION TO END_SECTION
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
            workspace.GetSymbolisedDocument(new Uri(contourPath));
            var mainDocument = workspace.GetSymbolUseExtractedDocument(new Uri(mainPath));

            var diagnostics = new DiagnosticHandler().HandleRequest(mainDocument, workspace).Items;

            Assert.AreEqual(
                0,
                mainDocument.ParserDiagnostics.Count,
                string.Join(Environment.NewLine, mainDocument.ParserDiagnostics.Select(diagnostic => diagnostic.Message)));
            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.StartsWith("Cannot resolve symbol", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void NumberedLProcedureCallResolvesNumberedProcedureDefinition()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-numbered-procedure-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var helperPath = Path.Combine(directory, "L601.SPF");
        var mainPath = Path.Combine(directory, "TEST_MAIN.MPF");
        File.WriteAllText(
            helperPath,
            @"PROC L601 PREPRO
M17
");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
L601
M30
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

            Assert.IsFalse(diagnostics.Any(diagnostic =>
                diagnostic.Message.Equals("Cannot resolve symbol L601.", StringComparison.Ordinal)));
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

    [TestMethod]
    public void NestedManufacturerCycleDoesNotRequireExternDeclaration()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-nested-cycle-" + Guid.NewGuid());
        var cycleDirectory = Path.Combine(directory, "CMA.DIR", "ATC");
        Directory.CreateDirectory(cycleDirectory);
        var helperPath = Path.Combine(cycleDirectory, "TEST_HELPER.SPF");
        var mainPath = Path.Combine(directory, "TEST_MAIN.MPF");
        File.WriteAllText(
            helperPath,
            @"PROC TEST_HELPER(REAL targetPos)
RET
ENDPROC
");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
DEF REAL mainPos
TEST_HELPER(mainPos)
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
    public void CaseDifferencesDoNotProduceDiagnostics()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-case-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var helperPath = Path.Combine(directory, "mixedcase.SPF");
        var mainPath = Path.Combine(directory, "TEST_MAIN.MPF");
        File.WriteAllText(
            helperPath,
            @"PROC MixedCase()
RET
ENDPROC
");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
mixedcase()
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
            var helperDocument = workspace.GetSymbolUseExtractedDocument(new Uri(helperPath));
            var mainDocument = workspace.GetSymbolUseExtractedDocument(new Uri(mainPath));

            var diagnostics = new DiagnosticHandler().HandleRequest(helperDocument, workspace).Items
                .Concat(new DiagnosticHandler().HandleRequest(mainDocument, workspace).Items)
                .ToArray();

            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.Contains("capitalisation", StringComparison.OrdinalIgnoreCase)));
            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.Contains("does not match file name", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void ProcedureWithoutParametersCanBeCalledWithoutParentheses()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-bare-call-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var helperPath = Path.Combine(directory, "MOVE_SAFE.SPF");
        var mainPath = Path.Combine(directory, "TEST_MAIN.MPF");
        File.WriteAllText(
            helperPath,
            @"PROC MOVE_SAFE()
RET
ENDPROC
");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
MOVE_SAFE
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
                    diagnostic.Message.Equals("Cannot resolve symbol MOVE_SAFE.", StringComparison.Ordinal)));
            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.Contains("arguments do not match", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void UndeclaredMachineAxisNameDoesNotProduceUnresolvedSymbolDiagnostic()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-axis-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var mainPath = Path.Combine(directory, "TEST_MAIN.MPF");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
DEF REAL limit
PF=-215
RAM_AXIS=-100 AX[configuredAxis]=50
limit=$MA_POS_LIMIT_PLUS[PF]
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
            var mainDocument = workspace.GetSymbolUseExtractedDocument(new Uri(mainPath));

            var diagnostics = new DiagnosticHandler().HandleRequest(mainDocument, workspace).Items;

            Assert.AreEqual(
                0,
                mainDocument.ParserDiagnostics.Count,
                string.Join(Environment.NewLine, mainDocument.ParserDiagnostics.Select(diagnostic => diagnostic.Message)));
            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.Equals("Cannot resolve symbol PF.", StringComparison.Ordinal)));
            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.Equals("Cannot resolve symbol RAM_AXIS.", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void OrdinaryAssignmentDoesNotMakeUndeclaredVariableAMachineAxis()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-variable-assignment-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var mainPath = Path.Combine(directory, "TEST_MAIN.MPF");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
DEF BOOL enabled
HMI_DIALOG_ACK=0
IF enabled
    IF HMI_DIALOG_ACK==1
        HMI_DIALOG_ACK=0
        GOTOF DONE
    ENDIF
ENDIF
DONE:
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
            var mainDocument = workspace.GetSymbolUseExtractedDocument(new Uri(mainPath));

            var diagnostics = new DiagnosticHandler().HandleRequest(mainDocument, workspace).Items;

            Assert.AreEqual(
                0,
                mainDocument.ParserDiagnostics.Count,
                string.Join(Environment.NewLine, mainDocument.ParserDiagnostics.Select(diagnostic => diagnostic.Message)));
            Assert.IsTrue(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.Equals(
                        "Cannot resolve symbol HMI_DIALOG_ACK.",
                        StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void ExactIsVarGuardAllowsOptionalVariableOnlyInsideTrueBranch()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-isvar-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var mainPath = Path.Combine(directory, "TEST_MAIN.MPF");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
IF ISVAR(""OPTIONAL_FLAG"")
    OPTIONAL_FLAG=1
ENDIF
IF (ISVAR(""PAREN_FLAG""))
    PAREN_FLAG=1
ENDIF
IF ISVAR(""OTHER_FLAG"")
    WRONG_FLAG=1
ENDIF
IF NOT ISVAR(""NEGATED_FLAG"")
    NEGATED_FLAG=1
ENDIF
IF ISVAR(""ELSE_FLAG"")
    MSG(""available"")
ELSE
    ELSE_FLAG=0
ENDIF
OPTIONAL_FLAG=0
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
            var mainDocument = workspace.GetSymbolUseExtractedDocument(new Uri(mainPath));

            var unresolvedDiagnostics = new DiagnosticHandler()
                .HandleRequest(mainDocument, workspace)
                .Items
                .Where(diagnostic =>
                    diagnostic.Message.StartsWith("Cannot resolve symbol ", StringComparison.Ordinal))
                .Select(diagnostic => diagnostic.Message)
                .ToArray();

            Assert.AreEqual(
                0,
                mainDocument.ParserDiagnostics.Count,
                string.Join(Environment.NewLine, mainDocument.ParserDiagnostics.Select(diagnostic => diagnostic.Message)));
            Assert.AreEqual(
                1,
                unresolvedDiagnostics.Count(message =>
                    message.Equals("Cannot resolve symbol OPTIONAL_FLAG.", StringComparison.Ordinal)));
            CollectionAssert.DoesNotContain(unresolvedDiagnostics, "Cannot resolve symbol PAREN_FLAG.");
            CollectionAssert.Contains(unresolvedDiagnostics, "Cannot resolve symbol WRONG_FLAG.");
            CollectionAssert.Contains(unresolvedDiagnostics, "Cannot resolve symbol NEGATED_FLAG.");
            CollectionAssert.Contains(unresolvedDiagnostics, "Cannot resolve symbol ELSE_FLAG.");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void SiemensAndBlumRuntimeSymbolsDoNotRequireWorkspaceDefinitions()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-runtime-symbols-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var mainPath = Path.Combine(directory, "TEST_MAIN.MPF");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
DEF INT result
CYCLE976()
cycle150()
BL9903()
bl9908()
result=_B_ERRNO
result=_b_ctool
MC_ACZ()
CYCLE_CUSTOM()
BL_TOOL()
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
            var mainDocument = workspace.GetSymbolUseExtractedDocument(new Uri(mainPath));

            var diagnostics = new DiagnosticHandler().HandleRequest(mainDocument, workspace).Items;
            var unresolvedIdentifiers = diagnostics
                .Where(diagnostic =>
                    diagnostic.Message.StartsWith("Cannot resolve symbol ", StringComparison.Ordinal))
                .Select(diagnostic => diagnostic.Message)
                .ToArray();

            Assert.AreEqual(
                0,
                mainDocument.ParserDiagnostics.Count,
                string.Join(Environment.NewLine, mainDocument.ParserDiagnostics.Select(diagnostic => diagnostic.Message)));
            CollectionAssert.DoesNotContain(unresolvedIdentifiers, "Cannot resolve symbol CYCLE976.");
            CollectionAssert.DoesNotContain(unresolvedIdentifiers, "Cannot resolve symbol cycle150.");
            CollectionAssert.DoesNotContain(unresolvedIdentifiers, "Cannot resolve symbol BL9903.");
            CollectionAssert.DoesNotContain(unresolvedIdentifiers, "Cannot resolve symbol bl9908.");
            CollectionAssert.DoesNotContain(unresolvedIdentifiers, "Cannot resolve symbol _B_ERRNO.");
            CollectionAssert.DoesNotContain(unresolvedIdentifiers, "Cannot resolve symbol _b_ctool.");
            CollectionAssert.Contains(unresolvedIdentifiers, "Cannot resolve symbol MC_ACZ.");
            CollectionAssert.Contains(unresolvedIdentifiers, "Cannot resolve symbol CYCLE_CUSTOM.");
            CollectionAssert.Contains(unresolvedIdentifiers, "Cannot resolve symbol BL_TOOL.");
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
