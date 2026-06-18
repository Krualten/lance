using Antlr4.Runtime.Tree;
using LanceServer.Core.Document;
using LanceServer.Core.Symbol;
using LanceServer.Parser;
using LanceServerTest.Core.Workspace;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LanceServerTest.Parser;

[TestClass]
public class ParserManagerTest
{
    public class SimpleParseTreeWalker
    {
        public List<string> Elements { get; } = new();
            
        public SimpleParseTreeWalker(IParseTree parseTree)
        {
            InitializeRecursive(parseTree);
        }

        void InitializeRecursive(IParseTree currentPosition)
        {
            if (currentPosition.ChildCount == 0)
            {
                Elements.Add(currentPosition.GetText());
            }
            else
            {
                for (var i = 0; i < currentPosition.ChildCount; i++)
                {
                    InitializeRecursive(currentPosition.GetChild(i));
                }
            }
        }
    }
        
    [TestMethod]
    public void ParseEmptyDocument()
    {
        // Arrange
        var expectedText = "<EOF>";

        var code = string.Empty;
        var document = new PreprocessedDocument(new DocumentInformationMock(new Uri("file:///testfile.spf"), ".spf", DocumentType.SubProcedure), code, code, new PlaceholderTable(new Dictionary<string, string>()), "");
        var parserManager = new ParserManager();

        // Act
        var actualParseTree = parserManager.Parse(document).ParseTree;

        // Assert
        Assert.AreEqual(expectedText, actualParseTree.GetText());
    }

    [TestMethod]
    public void ParseSimpleProcedure()
    {
        // Arrange
        var code = 
            @"proc testProcedure(int testparam)

                define definedMacro as 42
                def int declaredVariable
                def real definedVariable = 2.3

                if (definedMacro > definedVariable) or (testparam < 0)
                    declaredVariable = 7
                endif

                ret
                endproc";
        var document = new PreprocessedDocument(new DocumentInformationMock(new Uri("file:///testfile.spf"), ".spf", DocumentType.SubProcedure), code, code, new PlaceholderTable(new Dictionary<string, string>()), "");
        var parserManager = new ParserManager();

        // Act
        var actualParseTree = parserManager.Parse(document).ParseTree;

        // Assert
        var treeWalker = new SimpleParseTreeWalker(actualParseTree);
        var elementPosition = 0;
            
        Assert.AreEqual("proc", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("testProcedure", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("(", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("int", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("testparam", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual(")", treeWalker.Elements[elementPosition++]);
        elementPosition++; //newline
        elementPosition++; //newline
        Assert.AreEqual("define", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("definedMacro", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("as", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("42", treeWalker.Elements[elementPosition++]);
        elementPosition++; //newline
        Assert.AreEqual("def", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("int", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("declaredVariable", treeWalker.Elements[elementPosition++]);
        elementPosition++; //newline
        Assert.AreEqual("def", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("real", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("definedVariable", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("=", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("2.3", treeWalker.Elements[elementPosition++]);
        elementPosition++; //newline
        elementPosition++; //newline
        Assert.AreEqual("if", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("(", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("definedMacro", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual(">", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("definedVariable", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual(")", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("or", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("(", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("testparam", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("<", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("0", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual(")", treeWalker.Elements[elementPosition++]);
        elementPosition++; //newline
        Assert.AreEqual("declaredVariable", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("=", treeWalker.Elements[elementPosition++]);
        Assert.AreEqual("7", treeWalker.Elements[elementPosition++]);
        elementPosition++; //newline
        Assert.AreEqual("endif", treeWalker.Elements[elementPosition++]);
        elementPosition++; //newline
        elementPosition++; //newline
        Assert.AreEqual("ret", treeWalker.Elements[elementPosition++]);
        elementPosition++; //newline
        Assert.AreEqual("endproc", treeWalker.Elements[elementPosition]);
    }

    [TestMethod]
    public void SymbolTable_SimpleProcedure()
    {
        // Arrange
        var code = 
            @"proc testProcedure(int testparam)

                define definedMacro as 42
                def int declaredVariable
                def real definedVariable = 2.3

                if (definedMacro > definedVariable) or (testparam < 0)
                    declaredVariable = 7
                endif

                ret
                endproc";
        var preprocessedDocument = new PreprocessedDocument(new DocumentInformationMock(new Uri("file:///testfile.spf"), ".spf", DocumentType.SubProcedure), code, code, new PlaceholderTable(new Dictionary<string, string>()), "");
        var parserManager = new ParserManager();
        var parserResult = parserManager.Parse(preprocessedDocument);
        var document = new ParsedDocument(preprocessedDocument, parserResult.ParseTree, parserResult.Diagnostics);

        // Act
        var actualSymbols = parserManager.GetSymbolTableForDocument(document).ToList();

        // Assert
        var symbolPosition = 0;
            
        Assert.AreEqual("testparam", actualSymbols[symbolPosition++].Identifier);
        Assert.AreEqual("testProcedure", actualSymbols[symbolPosition++].Identifier);
        Assert.AreEqual("definedMacro", actualSymbols[symbolPosition++].Identifier);
        Assert.AreEqual("declaredVariable", actualSymbols[symbolPosition++].Identifier);
        Assert.AreEqual("definedVariable", actualSymbols[symbolPosition].Identifier);
    }

    [TestMethod]
    public void ParsingErrorTest()
    {
        // Arrange
        var code = 
            @"proc testProcedure(int testparam)

                define definedMacro as 42
                def int declaredVariable
                def real definedVariable = 2.3

                if (definedMacro > definedVariable) or (testparam < 0)
                    declaredVariable = 7
                endif

                ret
                endproc
                ";
        var preprocessedDocument = new PreprocessedDocument(new DocumentInformationMock(new Uri("file:///testfile.spf"), ".spf", DocumentType.SubProcedure), code, code, new PlaceholderTable(new Dictionary<string, string>()), "");
        var parserManager = new ParserManager();
        var parserResult = parserManager.Parse(preprocessedDocument);
        var document = new ParsedDocument(preprocessedDocument, parserResult.ParseTree, parserResult.Diagnostics);

        // Act
        var actualSymbols = parserManager.GetSymbolTableForDocument(document).ToList();

        // Assert
        var symbolPosition = 0;
            
        Assert.AreEqual("testparam", actualSymbols[symbolPosition++].Identifier);
        Assert.AreEqual("testProcedure", actualSymbols[symbolPosition++].Identifier);
        Assert.AreEqual("definedMacro", actualSymbols[symbolPosition++].Identifier);
        Assert.AreEqual("declaredVariable", actualSymbols[symbolPosition++].Identifier);
        Assert.AreEqual("definedVariable", actualSymbols[symbolPosition].Identifier);
    }

    [TestMethod]
    public void LiteralCallPathIsAttachedToFollowingProcedureUsesUntilReset()
    {
        var code =
            @"CALLPATH(""/_N_WKS_DIR/_N_LIBRARY_WPD"")
HELPER()
CALLPATH()
OTHER()
";
        var preprocessedDocument = new PreprocessedDocument(
            new DocumentInformationMock(new Uri("file:///MAIN.MPF"), ".mpf", DocumentType.MainProcedure),
            code,
            code,
            new PlaceholderTable(new Dictionary<string, string>()),
            "");
        var parserManager = new ParserManager();
        var parserResult = parserManager.Parse(preprocessedDocument);
        var parsedDocument = new ParsedDocument(
            preprocessedDocument,
            parserResult.ParseTree,
            parserResult.Diagnostics);
        var symbolisedDocument = new SymbolisedDocument(parsedDocument, new SymbolTable());

        var procedureUses = parserManager
            .GetSymbolUseForDocument(symbolisedDocument)
            .OfType<ProcedureUse>()
            .ToList();

        Assert.AreEqual(0, parserResult.Diagnostics.Count);
        Assert.AreEqual(2, procedureUses.Count);
        Assert.AreEqual("/_N_WKS_DIR/_N_LIBRARY_WPD", procedureUses[0].CallPath);
        Assert.IsNull(procedureUses[1].CallPath);
    }

    [TestMethod]
    public void LiteralIndirectCallCreatesProcedureUseWithExplicitPath()
    {
        var code = @"CALL ""/_N_WKS_DIR/_N_LIBRARY_WPD/_N_TEST_HELPER_SPF""" + Environment.NewLine;
        var preprocessedDocument = new PreprocessedDocument(
            new DocumentInformationMock(new Uri("file:///MAIN.MPF"), ".mpf", DocumentType.MainProcedure),
            code,
            code,
            new PlaceholderTable(new Dictionary<string, string>()),
            "");
        var parserManager = new ParserManager();
        var parserResult = parserManager.Parse(preprocessedDocument);
        var parsedDocument = new ParsedDocument(
            preprocessedDocument,
            parserResult.ParseTree,
            parserResult.Diagnostics);
        var symbolisedDocument = new SymbolisedDocument(parsedDocument, new SymbolTable());

        var procedureUse = parserManager
            .GetSymbolUseForDocument(symbolisedDocument)
            .OfType<ProcedureUse>()
            .Single();

        Assert.AreEqual(
            0,
            parserResult.Diagnostics.Count,
            string.Join(Environment.NewLine, parserResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.AreEqual("TEST_HELPER", procedureUse.Identifier);
        Assert.AreEqual("/_N_WKS_DIR/_N_LIBRARY_WPD", procedureUse.ExplicitDirectoryPath);
        Assert.AreEqual(0, procedureUse.Arguments.Length);
    }

    [TestMethod]
    public void ModalCallWithParametersCreatesProcedureUse()
    {
        var code =
            @"MCALL TEST_HELPER(mainAxis, mainPos)
MCALL
";
        var preprocessedDocument = new PreprocessedDocument(
            new DocumentInformationMock(new Uri("file:///MAIN.MPF"), ".mpf", DocumentType.MainProcedure),
            code,
            code,
            new PlaceholderTable(new Dictionary<string, string>()),
            "");
        var parserManager = new ParserManager();
        var parserResult = parserManager.Parse(preprocessedDocument);
        var parsedDocument = new ParsedDocument(
            preprocessedDocument,
            parserResult.ParseTree,
            parserResult.Diagnostics);
        var symbolisedDocument = new SymbolisedDocument(parsedDocument, new SymbolTable());

        var procedureUses = parserManager
            .GetSymbolUseForDocument(symbolisedDocument)
            .OfType<ProcedureUse>()
            .ToList();

        Assert.AreEqual(
            0,
            parserResult.Diagnostics.Count,
            string.Join(Environment.NewLine, parserResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.AreEqual(1, procedureUses.Count);
        Assert.AreEqual("TEST_HELPER", procedureUses[0].Identifier);
        Assert.AreEqual(2, procedureUses[0].Arguments.Length);
    }

    [TestMethod]
    public void AbsolutePcallCreatesSingleProcedureUseWithPathAndParameters()
    {
        var code = @"PCALL/_N_WKS_DIR/_N_LIBRARY_WPD/TEST_HELPER(mainAxis, mainPos)" + Environment.NewLine;
        var symbolUses = GetSymbolUses(code, out var parserDiagnostics);
        var procedureUse = symbolUses.OfType<ProcedureUse>().Single();

        Assert.AreEqual(
            0,
            parserDiagnostics.Count,
            string.Join(Environment.NewLine, parserDiagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.AreEqual("TEST_HELPER", procedureUse.Identifier);
        Assert.AreEqual("/_N_WKS_DIR/_N_LIBRARY_WPD", procedureUse.ExplicitDirectoryPath);
        Assert.IsNull(procedureUse.ExplicitFileExtension);
        Assert.AreEqual(2, procedureUse.Arguments.Length);
        Assert.IsTrue(procedureUse.SupportsParameterTransferWithoutExtern);
    }

    [TestMethod]
    public void PcallWithoutPathBehavesLikeStandardProcedureCall()
    {
        var code = @"PCALL TEST_HELPER(mainAxis)" + Environment.NewLine;
        var symbolUses = GetSymbolUses(code, out var parserDiagnostics);
        var procedureUse = symbolUses.OfType<ProcedureUse>().Single();

        Assert.AreEqual(0, parserDiagnostics.Count);
        Assert.AreEqual("TEST_HELPER", procedureUse.Identifier);
        Assert.IsNull(procedureUse.ExplicitDirectoryPath);
        Assert.AreEqual(1, procedureUse.Arguments.Length);
        Assert.IsFalse(procedureUse.SupportsParameterTransferWithoutExtern);
    }

    [TestMethod]
    public void PcallWithExplicitNcFileIdentifierPreservesExtensionAndExternRequirement()
    {
        var code = @"PCALL/_N_WKS_DIR/_N_LIBRARY_WPD/_N_TEST_HELPER_CYC(mainAxis)" + Environment.NewLine;
        var symbolUses = GetSymbolUses(code, out var parserDiagnostics);
        var procedureUse = symbolUses.OfType<ProcedureUse>().Single();

        Assert.AreEqual(0, parserDiagnostics.Count);
        Assert.AreEqual("TEST_HELPER", procedureUse.Identifier);
        Assert.AreEqual("/_N_WKS_DIR/_N_LIBRARY_WPD", procedureUse.ExplicitDirectoryPath);
        Assert.AreEqual(".cyc", procedureUse.ExplicitFileExtension);
        Assert.IsFalse(procedureUse.SupportsParameterTransferWithoutExtern);
    }

    [TestMethod]
    public void ExplicitNcFileIdentifierAndExternDeclarationShareNormalizedName()
    {
        var code =
            @"EXTERN _N_TEST_HELPER_SPF(INT)
PCALL/_N_WKS_DIR/_N_LIBRARY_WPD/_N_TEST_HELPER_SPF(mainAxis)
";
        var symbolUses = GetSymbolUses(code, out var parserDiagnostics);

        Assert.AreEqual(0, parserDiagnostics.Count);
        Assert.AreEqual(
            "TEST_HELPER",
            symbolUses.OfType<DeclarationProcedureUse>().Single().Identifier);
        Assert.AreEqual(
            "TEST_HELPER",
            symbolUses.OfType<ProcedureUse>().Single().Identifier);
    }

    [TestMethod]
    public void LiteralIsoCallCreatesProcedureUseWithExplicitPath()
    {
        var code = @"ISOCALL ""/_N_WKS_DIR/_N_LIBRARY_WPD/_N_ISO_HELPER_SPF""" + Environment.NewLine;
        var preprocessedDocument = new PreprocessedDocument(
            new DocumentInformationMock(new Uri("file:///MAIN.MPF"), ".mpf", DocumentType.MainProcedure),
            code,
            code,
            new PlaceholderTable(new Dictionary<string, string>()),
            "");
        var parserManager = new ParserManager();
        var parserResult = parserManager.Parse(preprocessedDocument);
        var parsedDocument = new ParsedDocument(
            preprocessedDocument,
            parserResult.ParseTree,
            parserResult.Diagnostics);
        var symbolisedDocument = new SymbolisedDocument(parsedDocument, new SymbolTable());

        var procedureUse = parserManager
            .GetSymbolUseForDocument(symbolisedDocument)
            .OfType<ProcedureUse>()
            .Single();

        Assert.AreEqual(
            0,
            parserResult.Diagnostics.Count,
            string.Join(Environment.NewLine, parserResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.AreEqual("ISO_HELPER", procedureUse.Identifier);
        Assert.AreEqual("/_N_WKS_DIR/_N_LIBRARY_WPD", procedureUse.ExplicitDirectoryPath);
        Assert.AreEqual(".spf", procedureUse.ExplicitFileExtension);
    }

    [TestMethod]
    public void VariableIsoCallRemainsDynamicVariableUse()
    {
        var code = @"ISOCALL programName" + Environment.NewLine;
        var preprocessedDocument = new PreprocessedDocument(
            new DocumentInformationMock(new Uri("file:///MAIN.MPF"), ".mpf", DocumentType.MainProcedure),
            code,
            code,
            new PlaceholderTable(new Dictionary<string, string>()),
            "");
        var parserManager = new ParserManager();
        var parserResult = parserManager.Parse(preprocessedDocument);
        var parsedDocument = new ParsedDocument(
            preprocessedDocument,
            parserResult.ParseTree,
            parserResult.Diagnostics);
        var symbolisedDocument = new SymbolisedDocument(parsedDocument, new SymbolTable());

        var symbolUses = parserManager.GetSymbolUseForDocument(symbolisedDocument).ToList();

        Assert.AreEqual(0, parserResult.Diagnostics.Count);
        Assert.AreEqual(0, symbolUses.OfType<ProcedureUse>().Count());
        Assert.IsTrue(symbolUses.Any(use => use is SymbolUse && use.Identifier == "programName"));
    }

    [TestMethod]
    public void LiteralExternalCallCreatesProcedureUseWithPath()
    {
        var code = @"EXTCALL(""external/programs/EXT_HELPER.SPF"")" + Environment.NewLine;
        var preprocessedDocument = new PreprocessedDocument(
            new DocumentInformationMock(new Uri("file:///MAIN.MPF"), ".mpf", DocumentType.MainProcedure),
            code,
            code,
            new PlaceholderTable(new Dictionary<string, string>()),
            "");
        var parserManager = new ParserManager();
        var parserResult = parserManager.Parse(preprocessedDocument);
        var parsedDocument = new ParsedDocument(
            preprocessedDocument,
            parserResult.ParseTree,
            parserResult.Diagnostics);
        var symbolisedDocument = new SymbolisedDocument(parsedDocument, new SymbolTable());

        var procedureUse = parserManager
            .GetSymbolUseForDocument(symbolisedDocument)
            .OfType<ProcedureUse>()
            .Single();

        Assert.AreEqual(
            0,
            parserResult.Diagnostics.Count,
            string.Join(Environment.NewLine, parserResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.AreEqual("EXT_HELPER", procedureUse.Identifier);
        Assert.AreEqual("/external/programs", procedureUse.ExplicitDirectoryPath);
        Assert.AreEqual(".spf", procedureUse.ExplicitFileExtension);
    }

    [TestMethod]
    public void VariableExternalCallRemainsDynamicVariableUse()
    {
        var code = @"EXTCALL(externalProgram)" + Environment.NewLine;
        var preprocessedDocument = new PreprocessedDocument(
            new DocumentInformationMock(new Uri("file:///MAIN.MPF"), ".mpf", DocumentType.MainProcedure),
            code,
            code,
            new PlaceholderTable(new Dictionary<string, string>()),
            "");
        var parserManager = new ParserManager();
        var parserResult = parserManager.Parse(preprocessedDocument);
        var parsedDocument = new ParsedDocument(
            preprocessedDocument,
            parserResult.ParseTree,
            parserResult.Diagnostics);
        var symbolisedDocument = new SymbolisedDocument(parsedDocument, new SymbolTable());

        var symbolUses = parserManager.GetSymbolUseForDocument(symbolisedDocument).ToList();

        Assert.AreEqual(0, parserResult.Diagnostics.Count);
        Assert.AreEqual(0, symbolUses.OfType<ProcedureUse>().Count());
        Assert.IsTrue(symbolUses.Any(use => use is SymbolUse && use.Identifier == "externalProgram"));
    }

    [TestMethod]
    public void CallBlockWithLiteralProgramCreatesProcedureAndLabelVariableUses()
    {
        var code = @"CALL ""CONTOUR"" BLOCK startLabel TO endLabel" + Environment.NewLine;
        var symbolUses = GetSymbolUses(code, out var parserDiagnostics);

        Assert.AreEqual(
            0,
            parserDiagnostics.Count,
            string.Join(Environment.NewLine, parserDiagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.AreEqual(1, symbolUses.OfType<ProcedureUse>().Count());
        Assert.AreEqual("CONTOUR", symbolUses.OfType<ProcedureUse>().Single().Identifier);
        CollectionAssert.AreEquivalent(
            new[] { "startLabel", "endLabel" },
            symbolUses.OfType<SymbolUse>().Select(use => use.Identifier).ToArray());
    }

    [TestMethod]
    public void LocalCallBlockOnlyCreatesLabelVariableUses()
    {
        var code = @"CALL BLOCK startLabel TO endLabel" + Environment.NewLine;
        var symbolUses = GetSymbolUses(code, out var parserDiagnostics);

        Assert.AreEqual(0, parserDiagnostics.Count);
        Assert.AreEqual(0, symbolUses.OfType<ProcedureUse>().Count());
        CollectionAssert.AreEquivalent(
            new[] { "startLabel", "endLabel" },
            symbolUses.OfType<SymbolUse>().Select(use => use.Identifier).ToArray());
    }

    [TestMethod]
    public void CallBlockWithDynamicProgramKeepsAllStringVariablesDynamic()
    {
        var code = @"CALL programName BLOCK startLabel TO endLabel" + Environment.NewLine;
        var symbolUses = GetSymbolUses(code, out var parserDiagnostics);

        Assert.AreEqual(0, parserDiagnostics.Count);
        Assert.AreEqual(0, symbolUses.OfType<ProcedureUse>().Count());
        CollectionAssert.AreEquivalent(
            new[] { "programName", "startLabel", "endLabel" },
            symbolUses.OfType<SymbolUse>().Select(use => use.Identifier).ToArray());
    }

    [TestMethod]
    public void OperateGroupMetadataDoesNotInterruptNcParsingOrCreateSymbolUses()
    {
        var code =
            @"GROUP_BEGIN(0, ""Read data"", metadataVariable, 0)
mainValue = sourceValue
GROUP_ADDEND(0, 0)
GROUP_END()
";
        var symbolUses = GetSymbolUses(code, out var parserDiagnostics);

        Assert.AreEqual(
            0,
            parserDiagnostics.Count,
            string.Join(Environment.NewLine, parserDiagnostics.Select(diagnostic => diagnostic.Message)));
        CollectionAssert.AreEquivalent(
            new[] { "mainValue", "sourceValue" },
            symbolUses.OfType<SymbolUse>().Select(use => use.Identifier).ToArray());
        Assert.AreEqual(0, symbolUses.OfType<ProcedureUse>().Count());
    }

    [TestMethod]
    public void IncompleteOperateGroupRemainsParseableWhileEditing()
    {
        var code =
            @"GROUP_BEGIN(0, ""Open group"", 0, 0)
mainValue = sourceValue
";
        var symbolUses = GetSymbolUses(code, out var parserDiagnostics);

        Assert.AreEqual(0, parserDiagnostics.Count);
        CollectionAssert.AreEquivalent(
            new[] { "mainValue", "sourceValue" },
            symbolUses.OfType<SymbolUse>().Select(use => use.Identifier).ToArray());
    }

    private static IList<AbstractSymbolUse> GetSymbolUses(
        string code,
        out IList<LspTypes.Diagnostic> parserDiagnostics)
    {
        var preprocessedDocument = new PreprocessedDocument(
            new DocumentInformationMock(new Uri("file:///MAIN.MPF"), ".mpf", DocumentType.MainProcedure),
            code,
            code,
            new PlaceholderTable(new Dictionary<string, string>()),
            "");
        var parserManager = new ParserManager();
        var parserResult = parserManager.Parse(preprocessedDocument);
        var parsedDocument = new ParsedDocument(
            preprocessedDocument,
            parserResult.ParseTree,
            parserResult.Diagnostics);
        var symbolisedDocument = new SymbolisedDocument(parsedDocument, new SymbolTable());
        parserDiagnostics = parserResult.Diagnostics;
        return parserManager.GetSymbolUseForDocument(symbolisedDocument).ToList();
    }
}
