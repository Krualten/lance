using LanceServer.Core.Configuration;
using LanceServer.Core.Configuration.DataModel;
using LanceServer.Core.Workspace;
using LanceServer.Parser;
using LanceServer.Preprocessor;
using LanceServer.RequestHandler.Diagnostic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LanceServerTest.Diagnostic;

[TestClass]
public class DiagnosticHandlerTest
{
    [DataTestMethod]
    [DataRow("cus.dir")]
    [DataRow("cma.dir")]
    [DataRow("cst.dir")]
    [DataRow("_N_CUS_DIR")]
    [DataRow("_N_CMA_DIR")]
    [DataRow("_N_CST_DIR")]
    public void CycleCallWithParametersDoesNotRequireExternDeclaration(string cycleDirectoryName)
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "lance-extern-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var cycleDirectory = Path.Combine(directory, cycleDirectoryName);
        Directory.CreateDirectory(cycleDirectory);
        var helperPath = Path.Combine(cycleDirectory, "TEST_HELPER.SPF");
        var mainPath = Path.Combine(directory, "TEST_MAIN.MPF");
        File.WriteAllText(
            helperPath,
            @"PROC TEST_HELPER(INT axisNo, REAL targetPos)
RET
ENDPROC
");
        File.WriteAllText(
            mainPath,
            @"PROC TEST_MAIN()
DEF INT mainAxis
DEF REAL mainPos
TEST_HELPER(mainAxis, mainPos)
RET
ENDPROC
");

        try
        {
            var configurationManager = CreateConfigurationManager();
            var workspace = new Workspace(
                new ParserManager(),
                new PlaceholderPreprocessor(configurationManager),
                configurationManager);
            var helperUri = new Uri(helperPath);
            var mainUri = new Uri(mainPath);
            workspace.GetSymbolisedDocument(helperUri);
            var mainDocument = workspace.GetSymbolUseExtractedDocument(mainUri);

            // Act
            var diagnostics = new DiagnosticHandler().HandleRequest(mainDocument, workspace).Items;

            // Assert
            Assert.AreEqual(
                0,
                mainDocument.ParserDiagnostics.Count,
                string.Join(Environment.NewLine, mainDocument.ParserDiagnostics.Select(diagnostic => diagnostic.Message)));
            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.StartsWith("Missing extern declaration", StringComparison.Ordinal)));
            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.Equals("Cannot resolve symbol TEST_HELPER.", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static ConfigurationManager CreateConfigurationManager()
    {
        var configuration = new ServerConfiguration
        {
            SymbolTableConfiguration = new SymbolTableConfiguration
            {
                DefinitionFileExtensions = new[] { ".def" },
                MainProcedureFileExtensions = new[] { ".mpf" },
                SubProcedureFileExtensions = new[] { ".spf" },
                ManufacturerCyclesDirectories = new[] { "cma.dir" }
            },
            PlaceholderPreprocessor = new CustomPreprocessorConfiguration
            {
                FileExtensions = Array.Empty<string>(),
                Placeholders = Array.Empty<string>(),
                PlaceholderType = PlaceholderType.String
            }
        };

        return new ConfigurationManager(new DocumentationConfiguration(), Array.Empty<Uri>(), configuration);
    }
}
