using LanceServer.Core.Configuration;
using LanceServer.Core.Configuration.DataModel;
using LanceServer.Core.Document;
using LanceServer.Core.Symbol;
using LanceServer.Core.Workspace;
using LanceServer.Parser;
using LanceServer.Preprocessor;
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
