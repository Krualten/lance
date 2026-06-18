using LanceServer.Core.Configuration.DataModel;
using LanceServer.Core.Document;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LanceServerTest.Core.Workspace;

[TestClass]
public class DocumentInformationTest
{
    [DataTestMethod]
    [DataRow("cus.dir")]
    [DataRow("cma.dir")]
    [DataRow("cst.dir")]
    [DataRow("_N_CUS_DIR")]
    [DataRow("_N_CMA_DIR")]
    [DataRow("_N_CST_DIR")]
    public void StandardSinumerikCycleDirectoriesAreClassifiedAsCycles(string directoryName)
    {
        var documentInformation = CreateDocumentInformation(directoryName, Array.Empty<string>());

        Assert.AreEqual(DocumentType.CycleSubProcedure, documentInformation.DocumentType);
    }

    [TestMethod]
    public void ConfiguredOemCycleDirectoryIsClassifiedAsCycle()
    {
        var documentInformation = CreateDocumentInformation("OEM_CYCLES", new[] { "oem_cycles" });

        Assert.AreEqual(DocumentType.CycleSubProcedure, documentInformation.DocumentType);
    }

    [TestMethod]
    public void NestedDirectoryBelowCycleDirectoryIsNotClassifiedAsCycle()
    {
        var path = Path.Combine(Path.GetTempPath(), "CMA.DIR", "ARCHIVE", "HELPER.SPF");
        var documentInformation = new DocumentInformation(new Uri(path), CreateConfiguration(Array.Empty<string>()));

        Assert.AreEqual(DocumentType.SubProcedure, documentInformation.DocumentType);
    }

    private static DocumentInformation CreateDocumentInformation(
        string directoryName,
        string[] configuredManufacturerCycleDirectories)
    {
        var path = Path.Combine(Path.GetTempPath(), directoryName, "HELPER.SPF");
        return new DocumentInformation(new Uri(path), CreateConfiguration(configuredManufacturerCycleDirectories));
    }

    private static SymbolTableConfiguration CreateConfiguration(string[] configuredManufacturerCycleDirectories)
    {
        return new SymbolTableConfiguration
        {
            DefinitionFileExtensions = new[] { ".def" },
            MainProcedureFileExtensions = new[] { ".mpf" },
            SubProcedureFileExtensions = new[] { ".spf" },
            ManufacturerCyclesDirectories = configuredManufacturerCycleDirectories
        };
    }
}
