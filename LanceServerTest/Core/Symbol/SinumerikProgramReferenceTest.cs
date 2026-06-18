using LanceServer.Core.Symbol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LanceServerTest.Core.Symbol;

[TestClass]
public class SinumerikProgramReferenceTest
{
    [TestMethod]
    public void ParsesAbsoluteNcProgramPath()
    {
        var success = SinumerikProgramReference.TryParse(
            "/_N_WKS_DIR/_N_LIBRARY_WPD/_N_TEST_HELPER_SPF",
            out var identifier,
            out var directoryPath,
            out var fileExtension);

        Assert.IsTrue(success);
        Assert.AreEqual("TEST_HELPER", identifier);
        Assert.AreEqual("/_N_WKS_DIR/_N_LIBRARY_WPD", directoryPath);
        Assert.AreEqual(".spf", fileExtension);
    }

    [DataTestMethod]
    [DataRow("TEST_HELPER", "TEST_HELPER")]
    [DataRow("TEST_HELPER.SPF", "TEST_HELPER")]
    [DataRow("_N_TEST_HELPER_SPF", "TEST_HELPER")]
    [DataRow("TEST_HELPER.CYC", "TEST_HELPER")]
    [DataRow("_N_TEST_HELPER_CPF", "TEST_HELPER")]
    public void ParsesProgramNameVariants(string reference, string expectedIdentifier)
    {
        var success = SinumerikProgramReference.TryParse(
            reference,
            out var identifier,
            out var directoryPath);

        Assert.IsTrue(success);
        Assert.AreEqual(expectedIdentifier, identifier);
        Assert.IsNull(directoryPath);
    }
}
