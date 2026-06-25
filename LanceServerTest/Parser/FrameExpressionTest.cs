using LanceServer.Core.Document;
using LanceServer.Core.Symbol;
using LanceServer.Parser;
using LanceServer.Preprocessor;
using LanceServerTest.Core.Workspace;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LanceServerTest.Parser;

[TestClass]
public class FrameExpressionTest
{
    [TestMethod]
    [DataRow("$P_PFRAME=CTRANS(X,10,Y,20,Z,30)")]
    [DataRow("$P_UIFR[1]=CTRANS(X,0,Y,0,Z,0):CROT(X,0,Y,0,Z,0)")]
    [DataRow("$P_IFRAME=CTRANS(X,1,Y,2,Z,3):CROT(Z,4):CROT(X,5)")]
    [DataRow("$P_UIFR[frameActual-1]=CSCALE()")]
    [DataRow("resultFrame=$P_PFRAME:CTRANS(Z,-5)")]
    public void FrameExpressionsAreParsed(string code)
    {
        var parserManager = new ParserManager();
        var document = CreatePreprocessedDocument(code + Environment.NewLine);

        var parserResult = parserManager.Parse(document);

        Assert.AreEqual(
            0,
            parserResult.Diagnostics.Count,
            string.Join(Environment.NewLine, parserResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
    }

    [TestMethod]
    [DataRow("CTRANS(X,1)")]
    [DataRow("CMIRROR(X,1)")]
    [DataRow("CSCALE()")]
    [DataRow("CROT(Z,90)")]
    [DataRow("CROTS(Z,90)")]
    [DataRow("CRPL(X,1)")]
    [DataRow("CTRANS(X,1):CROT(Z,90)")]
    public void FrameExpressionsInferFrameType(string expression)
    {
        var code =
            "PROC FRAME_HELPER(FRAME targetFrame)" + Environment.NewLine +
            $"FRAME_HELPER({expression})" + Environment.NewLine +
            "RET" + Environment.NewLine +
            "ENDPROC" + Environment.NewLine;
        var parserManager = new ParserManager();
        var preprocessedDocument = CreatePreprocessedDocument(code);
        var parserResult = parserManager.Parse(preprocessedDocument);
        var parsedDocument = new ParsedDocument(
            preprocessedDocument,
            parserResult.ParseTree,
            parserResult.Diagnostics);
        var symbolisedDocument = new SymbolisedDocument(
            parsedDocument,
            new SymbolTable());

        var argument = parserManager
            .GetSymbolUseForDocument(symbolisedDocument)
            .OfType<ProcedureUse>()
            .Single()
            .Arguments
            .Single();

        Assert.AreEqual(0, parserResult.Diagnostics.Count);
        Assert.AreEqual(DataType.Frame, argument.InferredDataType);
    }

    private static PreprocessedDocument CreatePreprocessedDocument(string code)
    {
        return new PreprocessedDocument(
            new DocumentInformationMock(
                new Uri("file:///FRAME_TEST.SPF"),
                ".spf",
                DocumentType.SubProcedure),
            code,
            code,
            new PlaceholderTable(new Dictionary<string, string>()),
            "");
    }
}
