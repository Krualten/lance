using LanceServer.Core.Document;
using LanceServer.Core.Symbol;
using LanceServer.Parser;
using LanceServer.Preprocessor;
using LanceServerTest.Core.Workspace;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LanceServerTest.Parser;

[TestClass]
public class NcAddressTest
{
    [DataTestMethod]
    [DataRow("T0")]
    [DataRow("T=savedToolName")]
    [DataRow("T=\"\"")]
    [DataRow("T[$C_TE]=0")]
    [DataRow("MTL=$C_MTL T[$C_TE]=$C_T")]
    [DataRow("D1")]
    [DataRow("D=edgeIni")]
    [DataRow("DL=$C_DL")]
    [DataRow("TOFFL=0")]
    [DataRow("MTL=$C_MTL T=$C_T")]
    [DataRow("D1 M17")]
    [DataRow("AX[vertAxis]=10 F=2000 FOC[vertAxis] FXST[vertAxis]=50")]
    [DataRow("G[8]=frameActual")]
    public void ToolAndMachineAddressesAreParsed(string block)
    {
        var result = new ParserManager().Parse(CreateDocument(block + Environment.NewLine));

        Assert.AreEqual(
            0,
            result.Diagnostics.Count,
            string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
    }

    [DataTestMethod]
    [DataRow("L601", "L601")]
    [DataRow("l611", "l611")]
    public void NumberedSubprogramCreatesProcedureUse(string block, string expectedIdentifier)
    {
        var parserManager = new ParserManager();
        var preprocessedDocument = CreateDocument(block + Environment.NewLine);
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

        Assert.AreEqual(0, parserResult.Diagnostics.Count);
        Assert.AreEqual(expectedIdentifier, procedureUse.Identifier);
        Assert.AreEqual(0, procedureUse.Arguments.Length);
    }

    [TestMethod]
    public void ParameterizedGGroupDoesNotCrashLanguageTokenExtraction()
    {
        var parserManager = new ParserManager();
        var preprocessedDocument = CreateDocument("G[8]=frameActual" + Environment.NewLine);
        var parserResult = parserManager.Parse(preprocessedDocument);
        var parsedDocument = new ParsedDocument(
            preprocessedDocument,
            parserResult.ParseTree,
            parserResult.Diagnostics);
        var symbolisedDocument = new SymbolisedDocument(parsedDocument, new SymbolTable());

        var tokens = parserManager.GetLanguageTokensForDocument(symbolisedDocument).ToList();

        Assert.AreEqual(0, parserResult.Diagnostics.Count);
        Assert.IsNotNull(tokens);
    }

    private static PreprocessedDocument CreateDocument(string code)
    {
        return new PreprocessedDocument(
            new DocumentInformationMock(
                new Uri("file:///NC_ADDRESS_TEST.SPF"),
                ".spf",
                DocumentType.CycleSubProcedure),
            code,
            code,
            new PlaceholderTable(new Dictionary<string, string>()),
            "");
    }
}
