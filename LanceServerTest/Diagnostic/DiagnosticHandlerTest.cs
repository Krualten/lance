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
    [TestMethod]
    public void ManufacturerCycleCallWithParametersDoesNotRequireExternDeclaration()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "lance-extern-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var manufacturerCyclesDirectory = Path.Combine(directory, "cma.dir");
        Directory.CreateDirectory(manufacturerCyclesDirectory);
        var helperPath = Path.Combine(manufacturerCyclesDirectory, "TEST_HELPER.SPF");
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
CALL TEST_HELPER(mainAxis, mainPos)
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
            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Message.StartsWith("Missing extern declaration", StringComparison.Ordinal)));
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
