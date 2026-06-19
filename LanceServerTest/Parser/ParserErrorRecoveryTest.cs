using LanceServer.Core.Document;
using LanceServer.Core.Symbol;
using LanceServer.Parser;
using LanceServerTest.Core.Workspace;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LanceServerTest.Parser;

[TestClass]
public class ParserErrorRecoveryTest
{
    [TestMethod]
    public void SymbolTable_IncompleteVariableDeclarationReportsParseErrorAndDoesNotThrow()
    {
        // Arrange
        var code = "def int";
        var preprocessedDocument = CreateDocument(code);
        var parserManager = new ParserManager();
        var parserResult = parserManager.Parse(preprocessedDocument);
        var document = new ParsedDocument(preprocessedDocument, parserResult.ParseTree, parserResult.Diagnostics);

        // Act
        var actualSymbols = parserManager.GetSymbolTableForDocument(document).ToList();

        // Assert
        Assert.IsTrue(parserResult.Diagnostics.Any());
        Assert.AreEqual("testfile", actualSymbols.Single().Identifier);
    }

    [TestMethod]
    public void SymbolUse_IncompleteExternDeclarationReportsParseErrorAndDoesNotThrow()
    {
        // Arrange
        var code = "extern";
        var preprocessedDocument = CreateDocument(code);
        var parserManager = new ParserManager();
        var parserResult = parserManager.Parse(preprocessedDocument);
        var parsedDocument = new ParsedDocument(preprocessedDocument, parserResult.ParseTree, parserResult.Diagnostics);
        var symbolisedDocument = new SymbolisedDocument(parsedDocument, new SymbolTable());

        // Act
        var actualSymbolUses = parserManager.GetSymbolUseForDocument(symbolisedDocument).ToList();

        // Assert
        Assert.IsTrue(parserResult.Diagnostics.Any());
        Assert.AreEqual(
            0,
            actualSymbolUses.Count,
            string.Join(", ", actualSymbolUses.Select(symbolUse => symbolUse.Identifier)));
    }

    private static PreprocessedDocument CreateDocument(string code)
    {
        return new PreprocessedDocument(
            new DocumentInformationMock(new Uri("file:///testfile.spf"), ".spf", DocumentType.SubProcedure),
            code,
            code,
            new PlaceholderTable(new Dictionary<string, string>()),
            "");
    }
}
