using LanceServer.Core.Document;
using LanceServer.Parser;
using LanceServer.Preprocessor;
using LanceServerTest.Core.Workspace;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LanceServerTest.Parser;

[TestClass]
public class ProcedureTerminationTest
{
    [TestMethod]
    [DataRow("RET")]
    [DataRow("M17")]
    [DataRow("M30")]
    [DataRow("M2")]
    [DataRow("REPOSA")]
    [DataRow("STOPRE")]
    public void ProcedureCanReachEndOfFileAfterAnyValidLastStatement(string lastStatement)
    {
        var code =
            "PROC TEST_CYCLE()" + Environment.NewLine +
            lastStatement + Environment.NewLine +
            Environment.NewLine;

        var result = new ParserManager().Parse(CreateDocument(code));

        Assert.AreEqual(
            0,
            result.Diagnostics.Count,
            string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
    }

    [TestMethod]
    public void EndProcRemainsSupported()
    {
        var code =
            "PROC TEST_CYCLE()" + Environment.NewLine +
            "RET" + Environment.NewLine +
            "ENDPROC" + Environment.NewLine;

        var result = new ParserManager().Parse(CreateDocument(code));

        Assert.AreEqual(
            0,
            result.Diagnostics.Count,
            string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
    }

    private static PreprocessedDocument CreateDocument(string code)
    {
        return new PreprocessedDocument(
            new DocumentInformationMock(
                new Uri("file:///TEST_CYCLE.SPF"),
                ".spf",
                DocumentType.CycleSubProcedure),
            code,
            code,
            new PlaceholderTable(new Dictionary<string, string>()),
            "");
    }
}
