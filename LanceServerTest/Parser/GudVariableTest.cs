using LanceServer.Core.Configuration;
using LanceServer.Core.Configuration.DataModel;
using LanceServer.Core.Document;
using LanceServer.Core.Symbol;
using LanceServer.Core.Workspace;
using LanceServer.Parser;
using LanceServer.Preprocessor;
using LanceServer.RequestHandler.Diagnostic;
using LanceServerTest.Core.Workspace;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace LanceServerTest.Parser;

[TestClass]
public class GudVariableTest
{
    [TestMethod]
    public void StandaloneAccessRightsDoNotPreventFollowingGudDeclaration()
    {
        // Arrange
        var code =
            @"APW 14 APR 17
            DEF NCK BOOL ABR_BGlobal[30]
";
        var document = CreateParsedDefinitionDocument(code, out var parserResult);
        var parserManager = new ParserManager();

        // Act
        var symbols = parserManager.GetSymbolTableForDocument(document).OfType<VariableSymbol>().ToList();
        var languageTokens = parserManager
            .GetLanguageTokensForDocument(new SymbolisedDocument(document, new SymbolTable()))
            .Select(token => token.Code)
            .ToList();

        // Assert
        Assert.AreEqual(
            0,
            parserResult.Diagnostics.Count,
            string.Join(Environment.NewLine, parserResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.AreEqual(1, symbols.Count);
        Assert.AreEqual("ABR_BGlobal", symbols[0].Identifier);
        StringAssert.StartsWith(symbols[0].Description, "global variable");
        CollectionAssert.Contains(languageTokens, "apw");
        CollectionAssert.Contains(languageTokens, "apr");
    }

    [TestMethod]
    public void GudDeclaredAfterStandaloneAccessRightsIsAvailableAcrossWorkspace()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "lance-gud-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var definitionPath = Path.Combine(directory, "MGUD.DEF");
        var cyclePath = Path.Combine(directory, "TEST.SPF");
        File.WriteAllText(
            definitionPath,
            "APW 14 APR 17" + Environment.NewLine +
            "DEF NCK BOOL ABR_BGlobal[30]" + Environment.NewLine);
        File.WriteAllText(cyclePath, "ABR_BGlobal[1] = TRUE" + Environment.NewLine);

        try
        {
            var configurationManager = CreateConfigurationManager();
            var preprocessor = CreatePreprocessor();
            var workspace = new Workspace(new ParserManager(), preprocessor.Object, configurationManager.Object);
            var definitionUri = new Uri(definitionPath);
            var cycleUri = new Uri(cyclePath);

            // Act
            workspace.GetSymbolisedDocument(definitionUri);
            var symbols = workspace.GetSymbols("ABR_BGlobal", cycleUri).ToList();

            // Assert
            Assert.AreEqual(1, symbols.Count);
            Assert.AreEqual(definitionUri, symbols[0].SourceDocument);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    [DataRow("DEF NCK REAL PHU 2 GD_ATC_Height_Clearance")]
    [DataRow("DEF NCK APWP 1 APRP 7 APWB 1 APRB 7 REAL PHU 4 LLI 0 _CAA_MAX_CUT_FEED=5001")]
    [DataRow("DEF NCK REAL PHY 2 GD_DOCUMENTED_UNIT")]
    public void GudPhysicalUnitModifiersAreParsed(string declaration)
    {
        var document = CreateParsedDefinitionDocument(
            declaration + Environment.NewLine,
            out var parserResult);
        var symbols = new ParserManager()
            .GetSymbolTableForDocument(document)
            .OfType<VariableSymbol>()
            .ToList();

        Assert.AreEqual(
            0,
            parserResult.Diagnostics.Count,
            string.Join(Environment.NewLine, parserResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.AreEqual(1, symbols.Count);
    }

    [TestMethod]
    public void GudWithPhuIsAvailableAcrossWorkspace()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lance-gud-phu-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var definitionPath = Path.Combine(directory, "MGUD_ATC.DEF");
        var cyclePath = Path.Combine(directory, "ATC_CHANGE.SPF");
        File.WriteAllText(
            definitionPath,
            "DEF NCK REAL PHU 2 GD_ATC_Height_Clearance" + Environment.NewLine);
        File.WriteAllText(
            cyclePath,
            "GD_ATC_Height_Clearance = 25.0" + Environment.NewLine);

        try
        {
            var configurationManager = CreateConfigurationManager();
            var preprocessor = CreatePreprocessor();
            var workspace = new Workspace(new ParserManager(), preprocessor.Object, configurationManager.Object);
            var definitionUri = new Uri(definitionPath);
            var cycleUri = new Uri(cyclePath);

            workspace.GetSymbolisedDocument(definitionUri);
            var cycleDocument = workspace.GetSymbolUseExtractedDocument(cycleUri);
            var diagnostics = new DiagnosticHandler()
                .HandleRequest(cycleDocument, workspace)
                .Items;

            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.Equals(
                        "Cannot resolve symbol GD_ATC_Height_Clearance.",
                        StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static ParsedDocument CreateParsedDefinitionDocument(string code, out ParserResult parserResult)
    {
        var preprocessedDocument = new PreprocessedDocument(
            new DocumentInformationMock(new Uri("file:///MGUD.DEF"), ".def", DocumentType.Definition),
            code,
            code,
            new PlaceholderTable(new Dictionary<string, string>()),
            "");
        var parserManager = new ParserManager();
        parserResult = parserManager.Parse(preprocessedDocument);
        return new ParsedDocument(preprocessedDocument, parserResult.ParseTree, parserResult.Diagnostics);
    }

    private static Mock<IConfigurationManager> CreateConfigurationManager()
    {
        var configurationManager = new Mock<IConfigurationManager>();
        configurationManager
            .SetupGet(manager => manager.SymbolTableConfiguration)
            .Returns(new SymbolTableConfiguration
            {
                DefinitionFileExtensions = new[] { ".def" },
                MainProcedureFileExtensions = new[] { ".mpf" },
                SubProcedureFileExtensions = new[] { ".spf" },
                ManufacturerCyclesDirectories = Array.Empty<string>()
            });
        return configurationManager;
    }

    private static Mock<IPlaceholderPreprocessor> CreatePreprocessor()
    {
        var preprocessor = new Mock<IPlaceholderPreprocessor>();
        preprocessor
            .Setup(processor => processor.Filter(It.IsAny<ReadDocument>()))
            .Returns((ReadDocument document) =>
                new PlaceholderPreprocessedDocument(
                    document,
                    document.RawContent,
                    new PlaceholderTable(new Dictionary<string, string>())));
        return preprocessor;
    }
}
