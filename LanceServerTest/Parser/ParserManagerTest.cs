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
    public void ExternDeclarationCapturesTypesAndReferenceParameters()
    {
        var code = "EXTERN TEST_HELPER(REAL, VAR INT, STRING[20], AXIS)" + Environment.NewLine;
        var symbolUses = GetSymbolUses(code, out var parserDiagnostics);
        var arguments = symbolUses.OfType<DeclarationProcedureUse>().Single().Arguments;

        Assert.AreEqual(0, parserDiagnostics.Count);
        CollectionAssert.AreEqual(
            new DataType?[] { DataType.Real, DataType.Int, DataType.String, DataType.Axis },
            arguments.Select(argument => argument.InferredDataType).ToArray());
        CollectionAssert.AreEqual(
            new[] { false, true, false, false },
            arguments.Select(argument => argument.IsWritableReference).ToArray());
    }

    [DataTestMethod]
    [DataRow("TEST_HELPER(firstValue,,thirdValue,)")]
    [DataRow("MCALL TEST_HELPER(firstValue,,thirdValue,)")]
    [DataRow("PCALL/_N_WKS_DIR/_N_LIBRARY_WPD/TEST_HELPER(firstValue,,thirdValue,)")]
    public void ProcedureCallsPreserveOmittedArgumentPositions(string call)
    {
        var symbolUses = GetSymbolUses(call + Environment.NewLine, out var parserDiagnostics);
        var arguments = symbolUses.OfType<ProcedureUse>().Single().Arguments;

        Assert.AreEqual(
            0,
            parserDiagnostics.Count,
            string.Join(Environment.NewLine, parserDiagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.AreEqual(4, arguments.Length);
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, arguments.Select(argument => argument.Position).ToArray());
        CollectionAssert.AreEqual(
            new[] { false, true, false, true },
            arguments.Select(argument => argument.IsOmitted).ToArray());
    }

    [TestMethod]
    public void EmptyParenthesesContainNoOmittedArgument()
    {
        var symbolUses = GetSymbolUses("TEST_HELPER()" + Environment.NewLine, out var parserDiagnostics);

        Assert.AreEqual(0, parserDiagnostics.Count);
        Assert.AreEqual(0, symbolUses.OfType<ProcedureUse>().Single().Arguments.Length);
    }

    [TestMethod]
    public void NestedFunctionCommasDoNotCreateAdditionalProcedureArguments()
    {
        var code = "TEST_HELPER(ATAN2(firstValue, secondValue),,thirdValue)" + Environment.NewLine;
        var symbolUses = GetSymbolUses(code, out var parserDiagnostics);
        var arguments = symbolUses.OfType<ProcedureUse>().Single().Arguments;

        Assert.AreEqual(0, parserDiagnostics.Count);
        Assert.AreEqual(3, arguments.Length);
        CollectionAssert.AreEqual(
            new[] { false, true, false },
            arguments.Select(argument => argument.IsOmitted).ToArray());
    }

    [TestMethod]
    public void ProcedureArgumentsCaptureSafeTypeInformation()
    {
        var code = @"TEST_HELPER(12, 3.5, TRUE, ""text"", X, localResult, R10)" + Environment.NewLine;
        var symbolUses = GetSymbolUses(code, out var parserDiagnostics);
        var arguments = symbolUses.OfType<ProcedureUse>().Single().Arguments;

        Assert.AreEqual(0, parserDiagnostics.Count);
        CollectionAssert.AreEqual(
            new DataType?[]
            {
                DataType.Int,
                DataType.Real,
                DataType.Bool,
                DataType.String,
                DataType.Axis,
                null,
                DataType.Real
            },
            arguments.Select(argument => argument.InferredDataType).ToArray());
        Assert.AreEqual("localResult", arguments[5].ReferencedIdentifier);
        Assert.IsTrue(arguments[5].IsWritableReference);
        Assert.IsTrue(arguments[6].IsWritableReference);
    }

    [TestMethod]
    public void IncompleteProcedureArgumentsDoNotCrashSymbolExtraction()
    {
        var symbolUses = GetSymbolUses(
            "TEST_HELPER(firstValue," + Environment.NewLine,
            out var parserDiagnostics);

        Assert.IsTrue(parserDiagnostics.Count > 0);
        Assert.IsNotNull(symbolUses);
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
    public void CallBlockWithLiteralProgramCreatesProcedureAndTargetLabelUses()
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
            symbolUses.OfType<BlockLabelUse>().Select(use => use.Identifier).ToArray());
        Assert.IsTrue(symbolUses.OfType<BlockLabelUse>()
            .All(use => use.TargetProgramIdentifier == "CONTOUR"));
    }

    [TestMethod]
    public void LocalCallBlockCreatesCurrentProgramLabelUses()
    {
        var code = @"CALL BLOCK startLabel TO endLabel" + Environment.NewLine;
        var symbolUses = GetSymbolUses(code, out var parserDiagnostics);

        Assert.AreEqual(0, parserDiagnostics.Count);
        Assert.AreEqual(0, symbolUses.OfType<ProcedureUse>().Count());
        CollectionAssert.AreEquivalent(
            new[] { "startLabel", "endLabel" },
            symbolUses.OfType<BlockLabelUse>().Select(use => use.Identifier).ToArray());
        Assert.IsTrue(symbolUses.OfType<BlockLabelUse>()
            .All(use => use.TargetProgramIdentifier == null));
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
    public void ConcatenatedIndirectCallRemainsDynamic()
    {
        var code = @"CALL ""ATC"" << sourceMag << ""_CHANGE""" + Environment.NewLine;
        var symbolUses = GetSymbolUses(code, out var parserDiagnostics);

        Assert.AreEqual(
            0,
            parserDiagnostics.Count,
            string.Join(Environment.NewLine, parserDiagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.AreEqual(0, symbolUses.OfType<ProcedureUse>().Count());
        CollectionAssert.AreEqual(
            new[] { "sourceMag" },
            symbolUses.OfType<SymbolUse>().Select(use => use.Identifier).ToArray());
    }

    [TestMethod]
    public void MessageTextAcceptsDocumentedTrailingConcatOperator()
    {
        var code = @"MSG(""$70400"" << maxSpeed * 0.5 <<)" + Environment.NewLine;
        var symbolUses = GetSymbolUses(code, out var parserDiagnostics);

        Assert.AreEqual(
            0,
            parserDiagnostics.Count,
            string.Join(Environment.NewLine, parserDiagnostics.Select(diagnostic => diagnostic.Message)));
        CollectionAssert.AreEqual(
            new[] { "maxSpeed" },
            symbolUses.OfType<SymbolUse>().Select(use => use.Identifier).ToArray());
    }

    [TestMethod]
    public void MessageAcceptsOptionalExecutionParameter()
    {
        var code = @"MSG(""Actual RPM: "" << spindleSpeed <<, 1)" + Environment.NewLine;
        var symbolUses = GetSymbolUses(code, out var parserDiagnostics);

        Assert.AreEqual(
            0,
            parserDiagnostics.Count,
            string.Join(Environment.NewLine, parserDiagnostics.Select(diagnostic => diagnostic.Message)));
        CollectionAssert.AreEqual(
            new[] { "spindleSpeed" },
            symbolUses.OfType<SymbolUse>().Select(use => use.Identifier).ToArray());
    }

    [TestMethod]
    public void TrailingConcatOutsideMessageRemainsInvalid()
    {
        var code = @"result = ""value"" <<" + Environment.NewLine;
        GetSymbolUses(code, out var parserDiagnostics);

        Assert.IsTrue(parserDiagnostics.Any(diagnostic =>
            diagnostic.Message.Contains(
                "only valid in an MSG message text",
                StringComparison.Ordinal)));
    }

    [TestMethod]
    public void TrailingConcatIsNotAcceptedInMessageExecutionParameter()
    {
        var code = @"MSG(""Machining part"", executionMode <<)" + Environment.NewLine;
        GetSymbolUses(code, out var parserDiagnostics);

        Assert.IsTrue(parserDiagnostics.Any(diagnostic =>
            diagnostic.Message.Contains(
                "only valid in an MSG message text",
                StringComparison.Ordinal)));
    }

    [DataTestMethod]
    [DataRow(
        @"[HEADER]
FILE_TYPE = VCS
FILE_VERSION = 7
")]
    [DataRow(
        @"// X axis compensation data
[EXX] // XTX
AXIS_LENGTH [L_U] = 5000
GRIDPOINTS = {
0 0
250 0
}
")]
    public void VcsCompensationDataStoredAsSpfIsNotParsedAsNcCode(string code)
    {
        var preprocessedDocument = new PreprocessedDocument(
            new DocumentInformationMock(new Uri("file:///VCS_X.SPF"), ".spf", DocumentType.SubProcedure),
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

        var symbols = parserManager.GetSymbolTableForDocument(parsedDocument).ToList();

        Assert.IsInstanceOfType(parserResult.ParseTree, typeof(NonNcDataContext));
        Assert.AreEqual(0, parserResult.Diagnostics.Count);
        Assert.AreEqual(0, symbols.Count);
    }

    [TestMethod]
    public void ExecutableVcsCycleIsStillParsedAsNcCode()
    {
        var code =
            @"PROC VCS_ON()
VCS_FILE_TABLE[0]=1
M17
";
        var preprocessedDocument = new PreprocessedDocument(
            new DocumentInformationMock(new Uri("file:///VCS_ON.SPF"), ".spf", DocumentType.SubProcedure),
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

        var symbols = parserManager.GetSymbolTableForDocument(parsedDocument).ToList();

        Assert.IsFalse(parserResult.ParseTree is NonNcDataContext);
        Assert.AreEqual(
            0,
            parserResult.Diagnostics.Count,
            string.Join(Environment.NewLine, parserResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.IsTrue(symbols.OfType<ProcedureSymbol>().Any(symbol => symbol.Identifier == "VCS_ON"));
    }

    [TestMethod]
    public void EmptyManufacturerSettingIsReportedAsWarningAndKeepsItsSymbolUse()
    {
        var code = @"GD_Nc4_Cal_L_Tc1 = ; configured during commissioning" + Environment.NewLine;
        var symbolUses = GetSymbolUses(code, out var parserDiagnostics);

        Assert.AreEqual(1, parserDiagnostics.Count);
        Assert.AreEqual(LspTypes.DiagnosticSeverity.Warning, parserDiagnostics.Single().Severity);
        Assert.IsTrue(parserDiagnostics.Single().Message.Contains(
            "Assignment has no value",
            StringComparison.Ordinal));
        CollectionAssert.AreEqual(
            new[] { "GD_Nc4_Cal_L_Tc1" },
            symbolUses.OfType<SymbolUse>().Select(use => use.Identifier).ToArray());
    }

    [TestMethod]
    public void FinalLabelDoesNotRequirePhysicalNewlineAtEndOfFile()
    {
        var code =
            @"START_SECTION:
M17
END_SECTION:";
        var preprocessedDocument = new PreprocessedDocument(
            new DocumentInformationMock(new Uri("file:///SETTINGS.SPF"), ".spf", DocumentType.SubProcedure),
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

        var labels = parserManager.GetSymbolTableForDocument(parsedDocument)
            .OfType<LabelSymbol>()
            .Select(symbol => symbol.Identifier)
            .ToArray();

        Assert.AreEqual(
            0,
            parserResult.Diagnostics.Count,
            string.Join(Environment.NewLine, parserResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
        CollectionAssert.AreEquivalent(
            new[] { "START_SECTION", "END_SECTION" },
            labels);
    }

    [TestMethod]
    public void NumberedProcedureDefinitionSupportsPreprocessingAttribute()
    {
        var code =
            @"PROC L601 PREPRO
M17
";
        var preprocessedDocument = new PreprocessedDocument(
            new DocumentInformationMock(new Uri("file:///L601.SPF"), ".spf", DocumentType.CycleSubProcedure),
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
        var symbols = parserManager.GetSymbolTableForDocument(parsedDocument);
        var symbolisedDocument = new SymbolisedDocument(parsedDocument, new SymbolTable());
        var symbolUses = parserManager.GetSymbolUseForDocument(symbolisedDocument);

        Assert.AreEqual(
            0,
            parserResult.Diagnostics.Count,
            string.Join(Environment.NewLine, parserResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.AreEqual("L601", symbols.OfType<ProcedureSymbol>().Single().Identifier);
        Assert.AreEqual(0, symbolUses.OfType<ProcedureUse>().Count());
    }

    [TestMethod]
    public void ProcedureDefinitionSupportsManufacturerRuntimeAttributes()
    {
        var code =
            @"PROC TOOL_MONITORING(AXIS loadAxis, REAL maxLoad) IPTRLOCK SBLOF DISPLOF ICYCOF
M17
";
        var preprocessedDocument = new PreprocessedDocument(
            new DocumentInformationMock(
                new Uri("file:///TOOL_MONITORING.SPF"),
                ".spf",
                DocumentType.CycleSubProcedure),
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
        var procedure = parserManager.GetSymbolTableForDocument(parsedDocument)
            .OfType<ProcedureSymbol>()
            .Single();

        Assert.AreEqual(
            0,
            parserResult.Diagnostics.Count,
            string.Join(Environment.NewLine, parserResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.AreEqual("TOOL_MONITORING", procedure.Identifier);
        Assert.AreEqual(2, procedure.Parameters.Length);
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
